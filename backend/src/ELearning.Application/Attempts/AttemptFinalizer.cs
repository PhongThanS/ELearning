using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Application.Exams;
using ELearning.Application.Grading;
using ELearning.Domain.Attempts;
using ELearning.Domain.Enums;
using ELearning.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Attempts;

/// <summary>
/// Kết thúc một lượt thi: khóa dòng → nộp → chấm → tạo ExamResult → audit, trong một transaction
/// (docs/02-nghiep-vu.md mục 6.7). Idempotent: lượt đã kết thúc thì không làm gì.
/// </summary>
public interface IAttemptFinalizer
{
    /// <summary>Tải và khóa lượt thi (UPDLOCK) kèm câu hỏi / câu trả lời. Phải gọi trong transaction.</summary>
    Task<ExamAttempt?> LoadForUpdateAsync(Guid attemptId, CancellationToken ct);

    /// <summary>Nộp và chấm một lượt đã được khóa; không SaveChanges.</summary>
    Task<ExamResult> FinalizeLockedAsync(ExamAttempt attempt, SubmitReason reason, string? ip, CancellationToken ct);

    /// <summary>Tự khóa, nộp, chấm và lưu trong transaction riêng. Trả về false nếu lượt không còn IN_PROGRESS.</summary>
    Task<bool> FinalizeAsync(Guid attemptId, SubmitReason reason, CancellationToken ct);
}

internal sealed class AttemptFinalizer(
    IAppDbContext db,
    IAttemptLock attemptLock,
    IGradingService grading,
    IAuditService audit,
    TimeProvider time,
    IOptions<ExamOptions> examOptions) : IAttemptFinalizer
{
    private TimeSpan Grace => TimeSpan.FromSeconds(examOptions.Value.SubmitGraceSeconds);

    public async Task<ExamAttempt?> LoadForUpdateAsync(Guid attemptId, CancellationToken ct)
    {
        await attemptLock.LockAsync(attemptId, ct);
        return await db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
            .SingleOrDefaultAsync(a => a.Id == attemptId, ct);
    }

    public async Task<ExamResult> FinalizeLockedAsync(ExamAttempt attempt, SubmitReason reason, string? ip, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        attempt.Submit(now, Grace, reason, ip);
        var score = await grading.GradeAsync(attempt, ct);
        var result = ExamResult.Create(attempt, score, now);
        db.ExamResults.Add(result);

        var action = attempt.SubmitReason switch
        {
            SubmitReason.Student => AuditActions.AttemptSubmitted,
            SubmitReason.ForcedByAdmin => AuditActions.AttemptForceSubmitted,
            _ => AuditActions.AttemptAutoSubmitted,
        };
        audit.Write(
            action,
            nameof(ExamAttempt),
            attempt.Id,
            newValue: new { attempt.Status, attempt.SubmitReason, score.TotalScore, score.MaxScore },
            userId: reason == SubmitReason.Student ? attempt.UserId : null);
        return result;
    }

    public Task<bool> FinalizeAsync(Guid attemptId, SubmitReason reason, CancellationToken ct) =>
        db.InTransactionAsync(
            async () =>
            {
                var attempt = await LoadForUpdateAsync(attemptId, ct);
                if (attempt is null || !attempt.IsInProgress)
                {
                    return false;
                }

                await FinalizeLockedAsync(attempt, reason, ip: null, ct);
                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);
}

/// <summary>
/// Tự nộp lượt quá hạn (D-05) và buộc nộp khi đóng đề (D-06).
/// Chuyển trạng thái nguyên tử nên chạy nhiều instance song song vẫn không chấm trùng.
/// </summary>
public interface IAttemptExpirationService
{
    Task<int> ProcessExpiredAsync(CancellationToken ct);
}

internal sealed class AttemptExpirationService(
    IAppDbContext db,
    IAttemptFinalizer finalizer,
    TimeProvider time,
    IOptions<ExamOptions> examOptions,
    ILogger<AttemptExpirationService> logger) : IAttemptExpirationService, IExamAttemptCloser
{
    public async Task<int> ProcessExpiredAsync(CancellationToken ct)
    {
        var options = examOptions.Value;
        var cutoff = time.GetUtcNow().UtcDateTime.AddSeconds(-options.SubmitGraceSeconds);
        var ids = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.Status == AttemptStatus.InProgress && a.ExpiredAt < cutoff)
            .OrderBy(a => a.ExpiredAt)
            .Select(a => a.Id)
            .Take(options.ExpirationSweepBatchSize)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await finalizer.FinalizeAsync(id, SubmitReason.TimeExpired, ct))
                {
                    processed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Một lượt lỗi không được chặn các lượt khác; vòng sau sẽ thử lại.
                logger.LogError(ex, "Tự nộp lượt thi {AttemptId} thất bại", id);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return processed;
    }

    public async Task<int> ForceSubmitInProgressAsync(Guid examId, CancellationToken ct)
    {
        var ids = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.ExamId == examId && a.Status == AttemptStatus.InProgress)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            if (await finalizer.FinalizeAsync(id, SubmitReason.ForcedByAdmin, ct))
            {
                processed++;
            }

            db.ChangeTracker.Clear();
        }

        return processed;
    }
}

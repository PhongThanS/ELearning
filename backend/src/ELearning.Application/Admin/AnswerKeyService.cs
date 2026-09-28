using System.Text.Json;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Grading;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Admin;

public interface IAnswerKeyService
{
    Task<Result<RegradeSummaryDto>> CorrectAnswerKeyAsync(
        Guid examId, Guid versionId, Guid examQuestionId, CorrectAnswerKeyRequest request, CancellationToken ct);

    Task<Result<RegradeSummaryDto>> VoidQuestionAsync(
        Guid examId, Guid versionId, Guid examQuestionId, AdminReasonRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<AnswerKeyCorrectionDto>>> ListCorrectionsAsync(Guid examId, CancellationToken ct);
}

/// <summary>
/// Sửa đáp án / hủy câu trên version đã publish rồi chấm lại mọi lượt đã nộp (D-11).
/// Câu trả lời của học viên không bao giờ bị sửa; điểm cũ được lưu vào ExamResultHistory.
/// </summary>
internal sealed class AnswerKeyService(
    IAppDbContext db,
    IGradingService grading,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CorrectAnswerKeyRequest> correctValidator,
    IValidator<AdminReasonRequest> reasonValidator) : IAnswerKeyService
{
    public const int RegradeBatchSize = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<RegradeSummaryDto>> CorrectAnswerKeyAsync(
        Guid examId, Guid versionId, Guid examQuestionId, CorrectAnswerKeyRequest request, CancellationToken ct)
    {
        var errors = await correctValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<RegradeSummaryDto>.Failure(errors);
        }

        return await ApplyAsync(
            examId,
            versionId,
            examQuestionId,
            AnswerKeyCorrectionType.AnswerKey,
            request.Reason,
            q => q.CorrectAnswerKey(
                request.CorrectOptionCodes?.Select(c => c.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal),
                request.AcceptedAnswers,
                request.CorrectAnswerNumber,
                request.NumericTolerance),
            AuditActions.AnswerKeyCorrected,
            ct);
    }

    public async Task<Result<RegradeSummaryDto>> VoidQuestionAsync(
        Guid examId, Guid versionId, Guid examQuestionId, AdminReasonRequest request, CancellationToken ct)
    {
        var errors = await reasonValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<RegradeSummaryDto>.Failure(errors);
        }

        return await ApplyAsync(
            examId,
            versionId,
            examQuestionId,
            AnswerKeyCorrectionType.Void,
            request.Reason,
            q => q.Void(currentUser.RequiredUserId, time.GetUtcNow().UtcDateTime),
            AuditActions.QuestionVoided,
            ct);
    }

    public async Task<Result<IReadOnlyList<AnswerKeyCorrectionDto>>> ListCorrectionsAsync(Guid examId, CancellationToken ct)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
        }

        var items = await db.AnswerKeyCorrections.AsNoTracking()
            .Join(db.ExamQuestions, c => c.ExamQuestionId, q => q.Id, (c, q) => new { c, q })
            .Join(db.ExamVersions, x => x.q.ExamVersionId, v => v.Id, (x, v) => new { x.c, x.q, v })
            .Where(x => x.v.ExamId == examId)
            .OrderByDescending(x => x.c.CorrectedAt)
            .Select(x => new AnswerKeyCorrectionDto(
                x.c.Id,
                x.q.Id,
                x.v.VersionNumber,
                x.q.QuestionOrder,
                x.c.CorrectionType,
                x.c.OldKeyJson,
                x.c.NewKeyJson,
                x.c.Reason,
                x.c.AffectedAttemptCount,
                x.c.CorrectedBy,
                db.Users.Where(u => u.Id == x.c.CorrectedBy).Select(u => u.FullName).Single(),
                x.c.CorrectedAt))
            .ToListAsync(ct);
        return items;
    }

    private async Task<Result<RegradeSummaryDto>> ApplyAsync(
        Guid examId,
        Guid versionId,
        Guid examQuestionId,
        AnswerKeyCorrectionType type,
        string reason,
        Action<ExamQuestion> change,
        string auditAction,
        CancellationToken ct)
    {
        var version = await db.ExamVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId && v.ExamId == examId, ct);
        if (version is null)
        {
            return Error.NotFound(message: "Không tìm thấy phiên bản đề.");
        }

        if (version.Status == ExamVersionStatus.Draft)
        {
            return Error.Business(ErrorCodes.InvalidStateTransition, "Bản nháp được sửa trực tiếp; chấm lại chỉ áp dụng cho phiên bản đã publish.");
        }

        var question = await db.ExamQuestions.Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .SingleOrDefaultAsync(q => q.Id == examQuestionId && q.ExamVersionId == versionId, ct);
        if (question is null)
        {
            return Error.NotFound(ErrorCodes.QuestionNotFound, "Câu hỏi không thuộc phiên bản này.");
        }

        // Bước 1: đổi đáp án + ghi lịch sử sửa (một transaction)
        var oldKey = JsonSerializer.Serialize(question.AnswerKeySnapshot(), Json);
        change(question);
        var correction = Domain.Exams.AnswerKeyCorrection.Create(
            question.Id, type, oldKey, JsonSerializer.Serialize(question.AnswerKeySnapshot(), Json), reason,
            currentUser.RequiredUserId, time.GetUtcNow().UtcDateTime);
        db.AnswerKeyCorrections.Add(correction);
        audit.Write(auditAction, nameof(ExamQuestion), question.Id, correction.OldKeyJson, correction.NewKeyJson, reason);
        await db.SaveChangesAsync(ct);

        // Bước 2: chấm lại theo lô, mỗi lô một transaction (docs/02-nghiep-vu.md mục 8)
        var attemptIds = await db.ExamResults.AsNoTracking()
            .Where(r => r.ExamVersionId == versionId)
            .OrderBy(r => r.SubmittedAt)
            .Select(r => r.AttemptId)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var batch in attemptIds.Chunk(RegradeBatchSize))
        {
            db.ChangeTracker.Clear();
            changed += await db.InTransactionAsync(
                async () =>
                {
                    var attempts = await db.ExamAttempts
                        .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
                        .Where(a => batch.Contains(a.Id))
                        .ToListAsync(ct);
                    var results = await db.ExamResults.Where(r => batch.Contains(r.AttemptId)).ToDictionaryAsync(r => r.AttemptId, ct);
                    var batchChanged = 0;
                    foreach (var attempt in attempts)
                    {
                        var score = await grading.GradeAsync(attempt, ct);
                        if (results[attempt.Id].Regrade(score, correction.Id, time.GetUtcNow().UtcDateTime))
                        {
                            batchChanged++;
                        }
                    }

                    await db.SaveChangesAsync(ct);
                    return batchChanged;
                },
                ct);
        }

        // Bước 3: ghi số lượt bị ảnh hưởng
        db.ChangeTracker.Clear();
        var saved = await db.AnswerKeyCorrections.SingleAsync(c => c.Id == correction.Id, ct);
        saved.SetAffectedAttempts(attemptIds.Count);
        audit.Write(
            AuditActions.ExamRegraded, nameof(ExamVersion), versionId,
            newValue: new { CorrectionId = correction.Id, Attempts = attemptIds.Count, Changed = changed });
        await db.SaveChangesAsync(ct);

        return new RegradeSummaryDto(correction.Id, attemptIds.Count, changed);
    }
}

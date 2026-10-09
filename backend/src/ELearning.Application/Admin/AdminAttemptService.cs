using ELearning.Application.Attempts;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Media;
using ELearning.Domain.Attempts;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Admin;

public interface IAdminAttemptService
{
    Task<Result<PagedResult<AdminAttemptListItemDto>>> ListAsync(Guid examId, AdminAttemptListQuery query, CancellationToken ct);

    Task<Result<AdminAttemptDetailDto>> GetAsync(Guid attemptId, CancellationToken ct);

    Task<Result<AdminAttemptDetailDto>> ExtendAsync(Guid attemptId, ExtendAttemptRequest request, CancellationToken ct);

    Task<Result<AdminAttemptDetailDto>> ForceSubmitAsync(Guid attemptId, AdminReasonRequest request, CancellationToken ct);

    Task<Result<AdminAttemptDetailDto>> CancelAsync(Guid attemptId, AdminReasonRequest request, CancellationToken ct);
}

/// <summary>Thao tác admin trên lượt thi (docs/02-nghiep-vu.md mục 9). Mọi thao tác bắt buộc có lý do và audit.</summary>
internal sealed class AdminAttemptService(
    IAppDbContext db,
    IAttemptFinalizer finalizer,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    MediaLinks mediaLinks,
    IValidator<ExtendAttemptRequest> extendValidator,
    IValidator<AdminReasonRequest> reasonValidator) : IAdminAttemptService
{
    private static readonly Error AttemptNotFound = Error.NotFound(ErrorCodes.AttemptNotFound, "Không tìm thấy lượt thi.");

    public async Task<Result<PagedResult<AdminAttemptListItemDto>>> ListAsync(
        Guid examId, AdminAttemptListQuery query, CancellationToken ct)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
        }

        var rows = db.ExamAttempts.AsNoTracking().Where(a => a.ExamId == examId)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { a, u });
        if (query.Status is { } status)
        {
            rows = rows.Where(x => x.a.Status == status);
        }

        if (query.UserId is { } userId)
        {
            rows = rows.Where(x => x.a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            rows = rows.Where(x => EF.Functions.ILike(x.u.UserName, kw) || EF.Functions.ILike(x.u.FullName, kw));
        }

        rows = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("username", false) => rows.OrderBy(x => x.u.UserName).ThenBy(x => x.a.AttemptNumber),
            ("username", true) => rows.OrderByDescending(x => x.u.UserName).ThenBy(x => x.a.AttemptNumber),
            ("startedat", false) => rows.OrderBy(x => x.a.StartedAt),
            _ => rows.OrderByDescending(x => x.a.StartedAt),
        };

        var total = await rows.CountAsync(ct);
        var items = await rows.Skip(query.Skip).Take(query.PageSize)
            .Select(x => new AdminAttemptListItemDto(
                x.a.Id,
                x.u.Id,
                x.u.UserName,
                x.u.FullName,
                x.a.AttemptNumber,
                db.ExamVersions.Where(v => v.Id == x.a.ExamVersionId).Select(v => v.VersionNumber).Single(),
                x.a.Status,
                x.a.SubmitReason,
                x.a.StartedAt,
                x.a.ExpiredAt,
                x.a.SubmittedAt,
                x.a.TimeExtensionMinutes,
                db.ExamResults.Where(r => r.AttemptId == x.a.Id).Select(r => (decimal?)r.TotalScore).SingleOrDefault(),
                db.ExamResults.Where(r => r.AttemptId == x.a.Id).Select(r => (decimal?)r.MaxScore).SingleOrDefault(),
                db.ExamResults.Where(r => r.AttemptId == x.a.Id).Select(r => (decimal?)r.Percentage).SingleOrDefault(),
                db.ExamResults.Where(r => r.AttemptId == x.a.Id).Select(r => r.Passed).SingleOrDefault(),
                db.AttemptEvents.Count(e => e.AttemptId == x.a.Id)))
            .ToListAsync(ct);

        return new PagedResult<AdminAttemptListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<AdminAttemptDetailDto>> GetAsync(Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
            .SingleOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt is null)
        {
            return AttemptNotFound;
        }

        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        var version = await db.ExamVersions.AsNoTracking().SingleAsync(v => v.Id == attempt.ExamVersionId, ct);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == attempt.UserId, ct);
        var result = await db.ExamResults.AsNoTracking().SingleOrDefaultAsync(r => r.AttemptId == attemptId, ct);
        var snapshot = await db.ExamQuestions.AsNoTracking()
            .Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .Where(q => q.ExamVersionId == attempt.ExamVersionId)
            .ToDictionaryAsync(q => q.Id, ct);
        var events = await db.AttemptEvents.AsNoTracking()
            .Where(e => e.AttemptId == attemptId)
            .OrderBy(e => e.ServerTime)
            .Take(AttemptEvent.MaxEventsPerAttempt)
            .Select(e => new AdminEventDto(e.EventType, e.ClientTime, e.ServerTime, e.IpAddress, e.Detail))
            .ToListAsync(ct);

        var answers = attempt.Questions.OrderBy(q => q.QuestionOrder).Select(q =>
        {
            var eq = snapshot[q.ExamQuestionId];
            return new AdminAnswerDto(
                q.Id,
                q.QuestionOrder,
                eq.Content,
                eq.QuestionType,
                eq.AnswerDataType,
                eq.Score,
                q.Answer.SelectedOptions.Select(o => o.OptionCode).Order(StringComparer.Ordinal).ToList(),
                q.Answer.AnswerText,
                q.Answer.IsAnswered,
                q.Answer.IsMarkedForReview,
                q.Answer.AnsweredAt,
                q.Answer.SaveCount,
                eq.Options.Where(o => o.IsCorrect).OrderBy(o => o.DisplayOrder).Select(o => o.OptionCode).ToList(),
                eq.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
                eq.CorrectAnswerNumber,
                q.Answer.IsCorrect,
                q.Answer.Score,
                eq.IsVoided);
        }).ToList();

        return new AdminAttemptDetailDto(
            attempt.Id, exam.Id, exam.Code, exam.Name, version.Id, version.VersionNumber,
            user.Id, user.UserName, user.FullName, attempt.AttemptNumber, attempt.Status, attempt.SubmitReason,
            attempt.StartedAt, attempt.ExpiredAt, attempt.SubmittedAt, attempt.TimeExtensionMinutes,
            attempt.CancelledAt, attempt.CancelReason, attempt.StartedIp, attempt.StartedUserAgent, attempt.SubmittedIp,
            result?.TotalScore, result?.MaxScore, result?.Percentage, result?.CorrectCount, result?.Passed, result?.GradingRevision,
            answers, events,
            mediaLinks.For(answers.Select(a => a.Content)));
    }

    public async Task<Result<AdminAttemptDetailDto>> ExtendAsync(Guid attemptId, ExtendAttemptRequest request, CancellationToken ct)
    {
        var errors = await extendValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AdminAttemptDetailDto>.Failure(errors);
        }

        var outcome = await db.InTransactionAsync<Result<bool>>(
            async () =>
            {
                var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                if (attempt is null)
                {
                    return AttemptNotFound;
                }

                if (!attempt.IsInProgress)
                {
                    return Error.Conflict(ErrorCodes.AttemptNotInProgress, "Chỉ gia hạn được lượt đang làm.");
                }

                var old = attempt.ExpiredAt;
                attempt.Extend(request.Minutes);
                audit.Write(
                    AuditActions.AttemptExtended, nameof(ExamAttempt), attempt.Id,
                    new { ExpiredAt = old }, new { attempt.ExpiredAt, request.Minutes }, request.Reason);
                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);

        return outcome.IsFailure ? Result<AdminAttemptDetailDto>.Failure(outcome.Errors) : await GetAfterChangeAsync(attemptId, ct);
    }

    public async Task<Result<AdminAttemptDetailDto>> ForceSubmitAsync(Guid attemptId, AdminReasonRequest request, CancellationToken ct)
    {
        var errors = await reasonValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AdminAttemptDetailDto>.Failure(errors);
        }

        var outcome = await db.InTransactionAsync<Result<bool>>(
            async () =>
            {
                var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                if (attempt is null)
                {
                    return AttemptNotFound;
                }

                if (!attempt.IsInProgress)
                {
                    return Error.Conflict(ErrorCodes.AttemptNotInProgress, "Lượt thi đã kết thúc.");
                }

                await finalizer.FinalizeLockedAsync(attempt, SubmitReason.ForcedByAdmin, currentUser.IpAddress, ct, request.Reason);
                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);

        return outcome.IsFailure ? Result<AdminAttemptDetailDto>.Failure(outcome.Errors) : await GetAfterChangeAsync(attemptId, ct);
    }

    public async Task<Result<AdminAttemptDetailDto>> CancelAsync(Guid attemptId, AdminReasonRequest request, CancellationToken ct)
    {
        var errors = await reasonValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AdminAttemptDetailDto>.Failure(errors);
        }

        var outcome = await db.InTransactionAsync<Result<bool>>(
            async () =>
            {
                var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                if (attempt is null)
                {
                    return AttemptNotFound;
                }

                var oldStatus = attempt.Status;
                attempt.Cancel(currentUser.RequiredUserId, request.Reason, time.GetUtcNow().UtcDateTime);
                audit.Write(
                    AuditActions.AttemptCancelled, nameof(ExamAttempt), attempt.Id,
                    new { Status = oldStatus }, new { attempt.Status }, request.Reason);
                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);

        return outcome.IsFailure ? Result<AdminAttemptDetailDto>.Failure(outcome.Errors) : await GetAfterChangeAsync(attemptId, ct);
    }

    private async Task<Result<AdminAttemptDetailDto>> GetAfterChangeAsync(Guid attemptId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return await GetAsync(attemptId, ct);
    }
}

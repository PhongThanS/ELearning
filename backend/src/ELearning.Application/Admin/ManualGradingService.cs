using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Grading;
using ELearning.Application.Media;
using ELearning.Domain.Enums;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Admin;

public sealed record ManualGradingQuery : PageRequest
{
    /// <summary>PENDING (mặc định): chưa chấm; GRADED: đã chấm; ALL: tất cả.</summary>
    public string? Status { get; init; }

    public Guid? ExamQuestionId { get; init; }
}

public sealed record ManualGradingItemDto(
    Guid AttemptId,
    Guid AttemptQuestionId,
    Guid UserId,
    string UserName,
    string FullName,
    int AttemptNumber,
    DateTime? SubmittedAt,
    int QuestionOrder,
    Guid ExamQuestionId,
    string Content,
    ContentFormat ContentFormat,
    string? Explanation,
    decimal MaxScore,
    string? AnswerText,
    decimal? ManualScore,
    string? ManualComment,
    DateTime? ManualGradedAt,
    IReadOnlyDictionary<string, string>? Media = null);

public sealed record ManualGradeRequest(decimal Score, string? Comment);

public sealed record ManualGradeResultDto(Guid AttemptId, decimal TotalScore, decimal MaxScore, decimal Percentage, bool? Passed, int PendingManualCount);

public interface IManualGradingService
{
    Task<Result<PagedResult<ManualGradingItemDto>>> ListAsync(Guid examId, ManualGradingQuery query, CancellationToken ct);

    Task<Result<ManualGradeResultDto>> GradeAsync(Guid attemptId, Guid attemptQuestionId, ManualGradeRequest request, CancellationToken ct);
}

/// <summary>
/// Chấm tay câu tự luận (docs/02-nghiep-vu.md mục 2.3). Chấm xong một câu thì chấm lại cả lượt thi từ snapshot
/// (D-01) để tổng điểm, số câu đúng và trạng thái đạt luôn nhất quán; điểm chấm tay nằm ở AttemptAnswer.
/// </summary>
internal sealed class ManualGradingService(
    IAppDbContext db,
    IAttemptLock attemptLock,
    IGradingService grading,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    MediaLinks mediaLinks) : IManualGradingService
{
    public async Task<Result<PagedResult<ManualGradingItemDto>>> ListAsync(Guid examId, ManualGradingQuery query, CancellationToken ct)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
        }

        var items =
            from aq in db.AttemptQuestions.AsNoTracking()
            join a in db.ExamAttempts.AsNoTracking() on aq.AttemptId equals a.Id
            join eq in db.ExamQuestions.AsNoTracking() on aq.ExamQuestionId equals eq.Id
            join u in db.Users.AsNoTracking() on a.UserId equals u.Id
            where a.ExamId == examId
                && (a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.AutoSubmitted)
                && eq.QuestionType == QuestionType.Essay
                && !eq.IsVoided
                && aq.Answer.IsAnswered
            select new { aq, a, eq, u };

        if (query.ExamQuestionId is { } examQuestionId)
        {
            items = items.Where(x => x.eq.Id == examQuestionId);
        }

        items = query.Status?.ToUpperInvariant() switch
        {
            "GRADED" => items.Where(x => x.aq.Answer.ManualScore != null),
            "ALL" => items,
            _ => items.Where(x => x.aq.Answer.ManualScore == null),
        };

        var total = await items.CountAsync(ct);
        var page = await items
            .OrderBy(x => x.a.SubmittedAt).ThenBy(x => x.aq.QuestionOrder)
            .Skip(query.Skip).Take(query.PageSize)
            .Select(x => new ManualGradingItemDto(
                x.a.Id,
                x.aq.Id,
                x.u.Id,
                x.u.UserName,
                x.u.FullName,
                x.a.AttemptNumber,
                x.a.SubmittedAt,
                x.aq.QuestionOrder,
                x.eq.Id,
                x.eq.Content,
                x.eq.ContentFormat,
                x.eq.Explanation,
                x.eq.Score,
                x.aq.Answer.AnswerText,
                x.aq.Answer.ManualScore,
                x.aq.Answer.ManualComment,
                x.aq.Answer.ManualGradedAt))
            .ToListAsync(ct);
        var withMedia = page.Select(i => i with { Media = mediaLinks.For(i.Content, i.Explanation) }).ToList();
        return new PagedResult<ManualGradingItemDto>(withMedia, query.Page, query.PageSize, total);
    }

    public async Task<Result<ManualGradeResultDto>> GradeAsync(
        Guid attemptId, Guid attemptQuestionId, ManualGradeRequest request, CancellationToken ct)
    {
        if (request.Comment is { Length: > 2000 })
        {
            return Error.Validation("COMMENT_TOO_LONG", "Nhận xét tối đa 2000 ký tự.", "comment");
        }

        return await db.InTransactionAsync<Result<ManualGradeResultDto>>(
            async () =>
            {
                // Khóa lượt thi như lưu đáp án / nộp bài / chấm lại (D-21)
                await attemptLock.LockAsync(attemptId, ct);
                var attempt = await db.ExamAttempts
                    .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
                    .SingleOrDefaultAsync(a => a.Id == attemptId, ct);
                var question = attempt?.Questions.FirstOrDefault(q => q.Id == attemptQuestionId);
                if (attempt is null || question is null)
                {
                    return Error.NotFound(ErrorCodes.AttemptNotFound, "Không tìm thấy câu trả lời.");
                }

                if (!attempt.IsFinished)
                {
                    return Error.Business(ErrorCodes.ResultNotAvailable, "Chỉ chấm tay lượt thi đã nộp.");
                }

                var snapshot = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == question.ExamQuestionId, ct);
                if (snapshot.QuestionType != QuestionType.Essay)
                {
                    return Error.Business("NOT_ESSAY_QUESTION", "Chỉ chấm tay câu tự luận.");
                }

                if (!question.Answer.IsAnswered)
                {
                    return Error.Business("ANSWER_EMPTY", "Học viên bỏ trống câu này (0 điểm), không cần chấm.");
                }

                var result = await db.ExamResults.SingleAsync(r => r.AttemptId == attemptId, ct);
                var old = new { question.Answer.ManualScore, question.Answer.ManualComment, result.TotalScore };
                var now = time.GetUtcNow().UtcDateTime;
                question.Answer.GradeManually(request.Score, snapshot.Score, request.Comment, currentUser.RequiredUserId, now);

                var score = await grading.GradeAsync(attempt, ct);
                result.ApplyManualGrading(score, now);
                audit.Write(
                    AuditActions.AnswerManuallyGraded,
                    "AttemptAnswer",
                    question.Answer.Id,
                    old,
                    new { question.Answer.ManualScore, question.Answer.ManualComment, result.TotalScore, AttemptId = attemptId },
                    request.Comment);
                await db.SaveChangesAsync(ct);
                return Result<ManualGradeResultDto>.Success(new ManualGradeResultDto(
                    attempt.Id, result.TotalScore, result.MaxScore, result.Percentage, result.Passed, result.PendingManualCount));
            },
            ct);
    }
}

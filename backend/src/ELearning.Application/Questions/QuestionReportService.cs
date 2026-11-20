using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Media;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Questions;

/// <summary>Học sinh báo câu hỏi lỗi: số câu trong lượt thi của mình (đúng số hiển thị trên trang kết quả).</summary>
public sealed record ReportQuestionRequest(int QuestionOrder, QuestionReportReason Reason, string? Note);

/// <summary>Báo lỗi của học sinh trong một lượt thi.</summary>
public sealed record MyQuestionReportDto(
    Guid Id,
    int QuestionOrder,
    QuestionReportReason Reason,
    string? Note,
    QuestionReportStatus Status,
    DateTime CreatedAt,
    string? ResolutionNote);

public sealed record QuestionReportListQuery : PageRequest
{
    /// <summary>Lọc câu có báo lỗi ở trạng thái này (mặc định: mọi trạng thái).</summary>
    public QuestionReportStatus? Status { get; init; }

    public Guid? ExamId { get; init; }

    /// <summary>Tìm theo mã / tên đề.</summary>
    public string? Keyword { get; init; }
}

/// <summary>Một câu (trong snapshot đề) có học sinh báo lỗi.</summary>
public sealed record ReportedQuestionDto(
    Guid ExamQuestionId,
    Guid ExamId,
    Guid ExamVersionId,
    string ExamCode,
    string ExamName,
    int VersionNumber,
    int QuestionOrder,
    string ContentPreview,
    QuestionType QuestionType,
    Guid? SourceQuestionId,
    bool IsVoided,
    int ReportCount,
    int OpenCount,
    DateTime LastReportedAt,
    IReadOnlyList<QuestionReportReason> Reasons);

public sealed record QuestionReportItemDto(
    Guid Id,
    Guid AttemptId,
    Guid UserId,
    string UserName,
    string FullName,
    int QuestionOrder,
    QuestionReportReason Reason,
    string? Note,
    QuestionReportStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolvedByName,
    string? ResolutionNote);

public sealed record ReportedQuestionOptionDto(string Code, string Content, bool IsCorrect);

/// <summary>Chi tiết câu bị báo lỗi kèm đáp án (chỉ cho người có quyền QuestionReport.Manage).</summary>
public sealed record ReportedQuestionDetailDto(
    ReportedQuestionDto Summary,
    string Content,
    ContentFormat ContentFormat,
    AnswerDataType? AnswerDataType,
    decimal Score,
    IReadOnlyList<ReportedQuestionOptionDto> Options,
    IReadOnlyList<string> AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    string? Explanation,
    IReadOnlyList<QuestionReportItemDto> Reports,
    IReadOnlyDictionary<string, string> Media);

/// <summary>Đóng mọi báo lỗi đang chờ của câu: RESOLVED (lỗi thật, đã sửa) hoặc REJECTED (không lỗi); OPEN = mở lại.</summary>
public sealed record ResolveQuestionReportsRequest(QuestionReportStatus Status, string? Note);

public sealed record ResolveQuestionReportsResult(int Changed);

public interface IQuestionReportService
{
    Task<Result<IReadOnlyList<MyQuestionReportDto>>> ListMineAsync(Guid userId, Guid attemptId, CancellationToken ct);

    Task<Result<MyQuestionReportDto>> ReportAsync(Guid userId, Guid attemptId, ReportQuestionRequest request, CancellationToken ct);

    Task<PagedResult<ReportedQuestionDto>> ListAsync(QuestionReportListQuery query, CancellationToken ct);

    Task<int> CountOpenQuestionsAsync(CancellationToken ct);

    Task<Result<ReportedQuestionDetailDto>> GetAsync(Guid examQuestionId, CancellationToken ct);

    Task<Result<ResolveQuestionReportsResult>> ResolveAsync(Guid examQuestionId, ResolveQuestionReportsRequest request, CancellationToken ct);
}

/// <summary>
/// Báo câu hỏi lỗi (D-46, docs/02-nghiep-vu.md mục 8.1). Học sinh chỉ báo được <b>sau khi đã nộp bài</b>
/// (lượt SUBMITTED / AUTO_SUBMITTED của chính mình; lượt người khác → 404). Người có quyền
/// <c>QuestionReport.Manage</c> xem danh sách câu bị báo (gom theo câu trong snapshot đề) và đánh dấu đã xử lý;
/// việc sửa đáp án / hủy câu vẫn làm qua <see cref="ELearning.Application.Admin.IAnswerKeyService"/> (D-11).
/// </summary>
internal sealed class QuestionReportService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    MediaLinks mediaLinks,
    IAttemptLock attemptLock,
    IValidator<ReportQuestionRequest> reportValidator,
    IValidator<ResolveQuestionReportsRequest> resolveValidator,
    TimeProvider time) : IQuestionReportService
{
    public const int PreviewLength = 160;

    private static readonly Error AttemptNotFound = Error.NotFound(ErrorCodes.AttemptNotFound, "Không tìm thấy lượt thi.");

    private static readonly Error ReportNotFound = Error.NotFound(ErrorCodes.QuestionReportNotFound, "Không có báo lỗi nào cho câu hỏi này.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<Result<IReadOnlyList<MyQuestionReportDto>>> ListMineAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        if (!await db.ExamAttempts.AnyAsync(a => a.Id == attemptId && a.UserId == userId, ct))
        {
            return AttemptNotFound;
        }

        var reports = await db.QuestionReports.AsNoTracking()
            .Where(r => r.AttemptId == attemptId && r.ReportedBy == userId)
            .OrderBy(r => r.QuestionOrder)
            .ToListAsync(ct);
        return reports.Select(ToMine).ToList();
    }

    public async Task<Result<MyQuestionReportDto>> ReportAsync(
        Guid userId, Guid attemptId, ReportQuestionRequest request, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.Id == attemptId && a.UserId == userId)
            .Select(a => new { a.Id, a.ExamId, a.Status })
            .SingleOrDefaultAsync(ct);
        if (attempt is null)
        {
            return AttemptNotFound;
        }

        // Đang thi thì không được báo lỗi (tránh dùng báo lỗi để hỏi bài); lượt đã hủy cũng không
        if (attempt.Status is not (AttemptStatus.Submitted or AttemptStatus.AutoSubmitted))
        {
            return Error.Business(ErrorCodes.QuestionReportNotAllowed, "Chỉ báo lỗi câu hỏi sau khi đã nộp bài.");
        }

        var errors = await reportValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<MyQuestionReportDto>.Failure(errors);
        }

        var examQuestionId = await db.AttemptQuestions.AsNoTracking()
            .Where(q => q.AttemptId == attemptId && q.QuestionOrder == request.QuestionOrder)
            .Select(q => (Guid?)q.ExamQuestionId)
            .SingleOrDefaultAsync(ct);
        if (examQuestionId is not { } questionId)
        {
            return Error.Validation("QUESTION_ORDER_INVALID", "Bài làm không có câu số này.", "questionOrder");
        }

        var report = await db.QuestionReports
            .SingleOrDefaultAsync(r => r.AttemptId == attemptId && r.ExamQuestionId == questionId, ct);
        if (report is null)
        {
            report = new QuestionReport(attempt.Id, attempt.ExamId, questionId, request.QuestionOrder, userId, request.Reason, request.Note, Now);
            db.QuestionReports.Add(report);
        }
        else if (!report.UpdateByReporter(request.Reason, request.Note, Now))
        {
            return Error.Business(ErrorCodes.QuestionReportClosed, "Báo lỗi của câu này đã được thầy / cô xử lý.");
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (attemptLock.IsUniqueViolation(ex))
        {
            // Hai lần bấm gửi cùng lúc: lần sau đụng UQ_QuestionReports_Attempt_Question
            return Error.Conflict(ErrorCodes.ConcurrencyConflict, "Câu này vừa được báo lỗi. Tải lại trang để xem.");
        }

        return ToMine(report);
    }

    public async Task<PagedResult<ReportedQuestionDto>> ListAsync(QuestionReportListQuery query, CancellationToken ct)
    {
        var reports = db.QuestionReports.AsNoTracking();
        if (query.Status is { } status)
        {
            reports = reports.Where(r => r.Status == status);
        }

        if (query.ExamId is { } examId)
        {
            reports = reports.Where(r => r.ExamId == examId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            reports = reports.Where(r => db.Exams.Any(e => e.Id == r.ExamId
                && (EF.Functions.ILike(e.Code, kw) || EF.Functions.ILike(e.Name, kw))));
        }

        var grouped = reports.GroupBy(r => r.ExamQuestionId)
            .Select(g => new { ExamQuestionId = g.Key, Last = g.Max(r => r.CreatedAt), Count = g.Count() });
        var total = await grouped.CountAsync(ct);
        var page = await grouped
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.Last).ThenBy(g => g.ExamQuestionId)
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(ct);

        var summaries = await SummariesAsync(page.Select(p => p.ExamQuestionId).ToList(), ct);
        var items = page.Where(p => summaries.ContainsKey(p.ExamQuestionId)).Select(p => summaries[p.ExamQuestionId]).ToList();
        return new PagedResult<ReportedQuestionDto>(items, query.Page, query.PageSize, total);
    }

    public Task<int> CountOpenQuestionsAsync(CancellationToken ct) =>
        db.QuestionReports.Where(r => r.Status == QuestionReportStatus.Open).Select(r => r.ExamQuestionId).Distinct().CountAsync(ct);

    public async Task<Result<ReportedQuestionDetailDto>> GetAsync(Guid examQuestionId, CancellationToken ct)
    {
        var summaries = await SummariesAsync([examQuestionId], ct);
        if (!summaries.TryGetValue(examQuestionId, out var summary))
        {
            return ReportNotFound;
        }

        var question = await db.ExamQuestions.AsNoTracking()
            .Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .SingleAsync(q => q.Id == examQuestionId, ct);

        var reports = await db.QuestionReports.AsNoTracking()
            .Where(r => r.ExamQuestionId == examQuestionId)
            .Join(db.Users, r => r.ReportedBy, u => u.Id, (r, u) => new { r, u })
            .OrderBy(x => x.r.Status == QuestionReportStatus.Open ? 0 : 1).ThenByDescending(x => x.r.CreatedAt)
            .Select(x => new QuestionReportItemDto(
                x.r.Id,
                x.r.AttemptId,
                x.u.Id,
                x.u.UserName,
                x.u.FullName,
                x.r.QuestionOrder,
                x.r.Reason,
                x.r.Note,
                x.r.Status,
                x.r.CreatedAt,
                x.r.ResolvedAt,
                db.Users.Where(v => v.Id == x.r.ResolvedBy).Select(v => v.FullName).FirstOrDefault(),
                x.r.ResolutionNote))
            .Take(PageRequest.MaxPageSize)
            .ToListAsync(ct);

        var options = question.Options.OrderBy(o => o.DisplayOrder)
            .Select(o => new ReportedQuestionOptionDto(o.OptionCode, o.Content, o.IsCorrect)).ToList();
        return new ReportedQuestionDetailDto(
            summary,
            question.Content,
            question.ContentFormat,
            question.AnswerDataType,
            question.Score,
            options,
            question.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
            question.CorrectAnswerNumber,
            question.Explanation,
            reports,
            mediaLinks.For([question.Content, question.Explanation, .. options.Select(o => o.Content)]));
    }

    public async Task<Result<ResolveQuestionReportsResult>> ResolveAsync(
        Guid examQuestionId, ResolveQuestionReportsRequest request, CancellationToken ct)
    {
        var errors = await resolveValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<ResolveQuestionReportsResult>.Failure(errors);
        }

        var reports = await db.QuestionReports.Where(r => r.ExamQuestionId == examQuestionId).ToListAsync(ct);
        if (reports.Count == 0)
        {
            return ReportNotFound;
        }

        var now = Now;
        var userId = currentUser.RequiredUserId;
        var changed = 0;
        if (request.Status == QuestionReportStatus.Open)
        {
            // Mở lại: mọi báo lỗi đã đóng của câu quay về chờ xử lý
            foreach (var report in reports.Where(r => !r.IsOpen))
            {
                report.Reopen(now);
                changed++;
            }

            audit.Write(AuditActions.QuestionReportReopened, nameof(ExamQuestion), examQuestionId, newValue: new { changed });
        }
        else
        {
            foreach (var report in reports.Where(r => r.IsOpen))
            {
                report.Resolve(request.Status, userId, request.Note, now);
                changed++;
            }

            audit.Write(
                AuditActions.QuestionReportResolved, nameof(ExamQuestion), examQuestionId,
                newValue: new { status = request.Status, changed }, reason: request.Note);
        }

        await db.SaveChangesAsync(ct);
        return new ResolveQuestionReportsResult(changed);
    }

    private async Task<Dictionary<Guid, ReportedQuestionDto>> SummariesAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var questions = await db.ExamQuestions.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Join(db.ExamVersions, q => q.ExamVersionId, v => v.Id, (q, v) => new { q, v })
            .Join(db.Exams, x => x.v.ExamId, e => e.Id, (x, e) => new
            {
                x.q.Id,
                ExamId = e.Id,
                VersionId = x.v.Id,
                e.Code,
                e.Name,
                x.v.VersionNumber,
                x.q.QuestionOrder,
                x.q.Content,
                x.q.QuestionType,
                x.q.SourceQuestionId,
                x.q.IsVoided,
            })
            .ToListAsync(ct);

        var stats = await db.QuestionReports.AsNoTracking()
            .Where(r => ids.Contains(r.ExamQuestionId))
            .GroupBy(r => new { r.ExamQuestionId, r.Reason, r.Status })
            .Select(g => new { g.Key.ExamQuestionId, g.Key.Reason, g.Key.Status, Count = g.Count(), Last = g.Max(r => r.CreatedAt) })
            .ToListAsync(ct);

        return questions.ToDictionary(q => q.Id, q =>
        {
            var mine = stats.Where(s => s.ExamQuestionId == q.Id).ToList();
            return new ReportedQuestionDto(
                q.Id,
                q.ExamId,
                q.VersionId,
                q.Code,
                q.Name,
                q.VersionNumber,
                q.QuestionOrder,
                Preview(q.Content),
                q.QuestionType,
                q.SourceQuestionId,
                q.IsVoided,
                mine.Sum(s => s.Count),
                mine.Where(s => s.Status == QuestionReportStatus.Open).Sum(s => s.Count),
                mine.Count > 0 ? mine.Max(s => s.Last) : default,
                mine.GroupBy(s => s.Reason).OrderByDescending(g => g.Sum(s => s.Count)).Select(g => g.Key).ToList());
        });
    }

    /// <summary>Đoạn đầu nội dung câu (bỏ xuống dòng, cắt gọn) để hiện trong bảng.</summary>
    internal static string Preview(string content)
    {
        var flat = string.Join(' ', content.Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return flat.Length <= PreviewLength ? flat : string.Concat(flat.AsSpan(0, PreviewLength), "…");
    }

    private static MyQuestionReportDto ToMine(QuestionReport r) =>
        new(r.Id, r.QuestionOrder, r.Reason, r.Note, r.Status, r.CreatedAt, r.ResolutionNote);
}

internal sealed class ReportQuestionRequestValidator : AbstractValidator<ReportQuestionRequest>
{
    public ReportQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionOrder).GreaterThan(0).WithErrorCode("QUESTION_ORDER_INVALID").WithMessage("Chọn câu hỏi bị lỗi.");
        RuleFor(x => x.Reason).IsInEnum().WithErrorCode("REASON_INVALID").WithMessage("Lý do báo lỗi không hợp lệ.");
        RuleFor(x => x.Note).MaximumLength(QuestionReport.MaxNoteLength)
            .WithErrorCode("NOTE_TOO_LONG").WithMessage($"Mô tả tối đa {QuestionReport.MaxNoteLength} ký tự.");
        RuleFor(x => x.Note).NotEmpty().When(x => x.Reason == QuestionReportReason.Other)
            .WithErrorCode("NOTE_REQUIRED").WithMessage("Hãy mô tả lỗi của câu hỏi.");
    }
}

internal sealed class ResolveQuestionReportsRequestValidator : AbstractValidator<ResolveQuestionReportsRequest>
{
    public ResolveQuestionReportsRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithErrorCode("STATUS_INVALID").WithMessage("Trạng thái xử lý không hợp lệ.");
        RuleFor(x => x.Note).MaximumLength(QuestionReport.MaxNoteLength)
            .WithErrorCode("NOTE_TOO_LONG").WithMessage($"Ghi chú tối đa {QuestionReport.MaxNoteLength} ký tự.");
    }
}

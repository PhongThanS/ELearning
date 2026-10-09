using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Application.Exams;
using ELearning.Application.Media;
using ELearning.Domain.Attempts;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Grading;
using ELearning.Domain.Results;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Attempts;

public interface IAttemptService
{
    Task<PagedResult<StudentExamListItemDto>> ListExamsAsync(Guid userId, StudentExamListQuery query, CancellationToken ct);

    Task<Result<StudentExamDetailDto>> GetExamAsync(Guid userId, Guid examId, CancellationToken ct);

    Task<Result<AttemptDto>> StartAsync(Guid userId, Guid examId, CancellationToken ct);

    Task<Result<AttemptDto>> GetAttemptAsync(Guid userId, Guid attemptId, CancellationToken ct);

    Task<Result<SaveAnswersResponse>> SaveAnswersAsync(Guid userId, Guid attemptId, SaveAnswersRequest request, CancellationToken ct);

    Task<Result<RecordEventsResponse>> RecordEventsAsync(Guid userId, Guid attemptId, RecordEventsRequest request, CancellationToken ct);

    Task<Result<StudentResultDto>> SubmitAsync(Guid userId, Guid attemptId, CancellationToken ct);

    Task<Result<StudentResultDto>> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct);

    Task<PagedResult<StudentHistoryItemDto>> HistoryAsync(Guid userId, StudentHistoryQuery query, CancellationToken ct);
}

/// <summary>Lượt thi của học viên (docs/02-nghiep-vu.md mục 5–7, docs/05-api.md mục 6.7).</summary>
internal sealed class AttemptService(
    IAppDbContext db,
    IAttemptLock attemptLock,
    IAttemptFinalizer finalizer,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    MediaLinks mediaLinks,
    IOptions<ExamOptions> examOptions,
    IValidator<SaveAnswersRequest> saveValidator,
    IValidator<RecordEventsRequest> eventsValidator) : IAttemptService
{
    private static readonly Error ExamNotFound = Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
    private static readonly Error AttemptNotFound = Error.NotFound(ErrorCodes.AttemptNotFound, "Không tìm thấy lượt thi.");
    private static readonly Error NotInProgress = Error.Conflict(ErrorCodes.AttemptNotInProgress, "Lượt thi đã kết thúc.");
    private static readonly Error Expired = Error.Business(ErrorCodes.AttemptExpired, "Lượt thi đã hết giờ và đã được tự động nộp.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private TimeSpan Grace => TimeSpan.FromSeconds(examOptions.Value.SubmitGraceSeconds);

    // ----- Danh sách đề của học viên -----

    public async Task<PagedResult<StudentExamListItemDto>> ListExamsAsync(Guid userId, StudentExamListQuery query, CancellationToken ct)
    {
        var exams = AccessibleExams(userId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            exams = exams.Where(e => EF.Functions.ILike(e.Code, kw) || EF.Functions.ILike(e.Name, kw));
        }

        if (query.ClassroomId is { } classroomId)
        {
            // Lọc theo lớp chỉ khi học viên thuộc lớp đó (không để lộ đề gán cho lớp khác)
            var member = await db.ClassroomStudents.AnyAsync(s => s.UserId == userId && s.ClassroomId == classroomId, ct);
            exams = member ? exams.Where(e => e.Assignments.Any(a => a.ClassroomId == classroomId)) : exams.Where(_ => false);
        }

        var total = await exams.CountAsync(ct);
        var page = await exams
            .OrderBy(e => e.StartAt == null ? 1 : 0).ThenByDescending(e => e.StartAt).ThenBy(e => e.Name)
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(ct);

        var states = await LoadUserExamStatesAsync(userId, page.Select(e => e.Id).ToList(), ct);
        var items = page.Select(e =>
        {
            var state = states[e.Id];
            return new StudentExamListItemDto(
                e.Id, e.Code, e.Name, e.StartAt, e.EndAt, state.Version.DurationMinutes, state.Version.QuestionCount ?? 0,
                state.Allowed, state.Used, Math.Max(0, state.Allowed - state.Used), state.InProgressId,
                state.OfficialScore, state.Availability);
        }).ToList();

        return new PagedResult<StudentExamListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<StudentExamDetailDto>> GetExamAsync(Guid userId, Guid examId, CancellationToken ct)
    {
        var exam = await AccessibleExams(userId).SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        var state = (await LoadUserExamStatesAsync(userId, [examId], ct))[examId];
        var attempts = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.ExamId == examId && a.UserId == userId)
            .OrderByDescending(a => a.AttemptNumber)
            .ToListAsync(ct);
        var results = await db.ExamResults.AsNoTracking()
            .Where(r => r.ExamId == examId && r.UserId == userId)
            .ToDictionaryAsync(r => r.AttemptId, ct);
        var versions = await db.ExamVersions.AsNoTracking()
            .Where(v => v.ExamId == examId)
            .ToDictionaryAsync(v => v.Id, ct);

        var summaries = attempts.Select(a =>
        {
            var version = versions[a.ExamVersionId];
            var visibility = ResultPolicy.Evaluate(
                a.Status, version.ScoreVisibility, version.ReviewPolicy, exam.EndAt, state.Used, state.Allowed, Now);
            results.TryGetValue(a.Id, out var r);
            var pending = r is { PendingManualCount: > 0 };
            var show = visibility.ScoreVisible && r is not null && !pending;
            return new StudentAttemptSummaryDto(
                a.Id, a.AttemptNumber, a.Status, a.StartedAt, a.ExpiredAt, a.SubmittedAt, show,
                show ? r!.TotalScore : null, show ? r!.MaxScore : null, show ? r!.Percentage : null, show ? r!.Passed : null, pending);
        }).ToList();

        return new StudentExamDetailDto(
            exam.Id, exam.Code, exam.Name, exam.Description, exam.Instructions, exam.StartAt, exam.EndAt,
            state.Version.DurationMinutes, state.Version.QuestionCount ?? 0, state.Version.MaxScore ?? 0, state.Version.PassPercentage,
            state.Allowed, state.Used, Math.Max(0, state.Allowed - state.Used), state.InProgressId, state.OfficialScore,
            state.Availability, summaries);
    }

    // ----- Bắt đầu -----

    public async Task<Result<AttemptDto>> StartAsync(Guid userId, Guid examId, CancellationToken ct)
    {
        var exam = await AccessibleExams(userId).SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        var inProgress = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.ExamId == examId && a.UserId == userId && a.Status == AttemptStatus.InProgress)
            .Select(a => (Guid?)a.Id)
            .SingleOrDefaultAsync(ct);
        if (inProgress is { } existingId)
        {
            return await GetAttemptAsync(userId, existingId, resumed: true, ct);
        }

        var state = (await LoadUserExamStatesAsync(userId, [examId], ct))[examId];
        switch (state.Availability)
        {
            // Request song song vừa tạo lượt giữa hai lần đọc → trả về lượt đó (D-07)
            case ExamAvailability.InProgress when state.InProgressId is { } racedId:
                return await GetAttemptAsync(userId, racedId, resumed: true, ct);
            case ExamAvailability.NoAttemptsLeft:
                return Error.Business(ErrorCodes.MaxAttemptsExceeded, "Bạn đã dùng hết số lượt thi.");
            case not ExamAvailability.Available:
                return Error.Business(ErrorCodes.ExamNotAvailable, "Đề thi hiện không mở.");
        }

        // Option cần cho việc xáo đáp án lúc bắt đầu (ExamAttempt.Start)
        var version = await db.ExamVersions.AsNoTracking().Include(v => v.Questions).ThenInclude(q => q.Options)
            .Include(v => v.PoolRules)
            .SingleAsync(v => v.ExamId == examId && v.Status == ExamVersionStatus.Published, ct);
        var nextNumber = (await db.ExamAttempts.Where(a => a.ExamId == examId && a.UserId == userId)
            .MaxAsync(a => (int?)a.AttemptNumber, ct) ?? 0) + 1;

        var attempt = ExamAttempt.Start(exam, version, userId, nextNumber, Now, currentUser.IpAddress, currentUser.UserAgent);
        db.ExamAttempts.Add(attempt);
        audit.Write(AuditActions.AttemptStarted, nameof(ExamAttempt), attempt.Id, newValue: new { examId, attempt.AttemptNumber });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (attemptLock.IsUniqueViolation(ex))
        {
            // Hai request start song song: request thua trả về lượt của request thắng (D-07).
            db.ChangeTracker.Clear();
            var winner = await db.ExamAttempts.AsNoTracking()
                .Where(a => a.ExamId == examId && a.UserId == userId && a.Status == AttemptStatus.InProgress)
                .Select(a => (Guid?)a.Id)
                .SingleOrDefaultAsync(ct);
            return winner is { } winnerId
                ? await GetAttemptAsync(userId, winnerId, resumed: true, ct)
                : Error.Conflict(ErrorCodes.ConcurrencyConflict, "Vui lòng thử lại.");
        }

        return await GetAttemptAsync(userId, attempt.Id, resumed: false, ct);
    }

    public Task<Result<AttemptDto>> GetAttemptAsync(Guid userId, Guid attemptId, CancellationToken ct) =>
        GetAttemptAsync(userId, attemptId, resumed: false, ct);

    // ----- Lưu đáp án (D-18, D-21) -----

    public async Task<Result<SaveAnswersResponse>> SaveAnswersAsync(
        Guid userId, Guid attemptId, SaveAnswersRequest request, CancellationToken ct)
    {
        var errors = await saveValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<SaveAnswersResponse>.Failure(errors);
        }

        var outcome = await db.InTransactionAsync<Result<SaveAnswersResponse>>(
            async () =>
            {
                var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                if (attempt is null || attempt.UserId != userId)
                {
                    return AttemptNotFound;
                }

                if (!attempt.IsInProgress)
                {
                    return NotInProgress;
                }

                if (attempt.IsExpired(Now, Grace))
                {
                    // Quá ân hạn: nộp luôn trong request này (docs/02-nghiep-vu.md mục 6.5)
                    await finalizer.FinalizeLockedAsync(attempt, SubmitReason.TimeExpired, currentUser.IpAddress, ct);
                    await db.SaveChangesAsync(ct);
                    return Expired;
                }

                var snapshot = await db.ExamQuestions.AsNoTracking().Include(q => q.Options)
                    .Where(q => q.ExamVersionId == attempt.ExamVersionId)
                    .ToDictionaryAsync(q => q.Id, ct);

                var parsed = new List<(AttemptQuestion Question, SaveAnswerItem Item, string? Text, decimal? Number)>();
                var itemErrors = new List<Error>();
                for (var i = 0; i < request.Answers.Count; i++)
                {
                    var item = request.Answers[i];
                    var question = attempt.Questions.FirstOrDefault(q => q.Id == item.QuestionId);
                    if (question is null)
                    {
                        itemErrors.Add(Error.Business(ErrorCodes.InvalidAnswerShape, "Câu hỏi không thuộc lượt thi.") with { Field = $"answers[{i}].questionId" });
                        continue;
                    }

                    var check = ValidateShape(snapshot[question.ExamQuestionId], item, $"answers[{i}]");
                    if (check.Error is not null)
                    {
                        itemErrors.Add(check.Error);
                        continue;
                    }

                    parsed.Add((question, item, check.Text, check.Number));
                }

                if (itemErrors.Count > 0)
                {
                    return Result<SaveAnswersResponse>.Failure(itemErrors);
                }

                var now = Now;
                var saved = parsed.Select(p => new SavedAnswerDto(
                    p.Question.Id,
                    p.Question.Answer.Apply(
                        (p.Item.SelectedOptions ?? []).Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList(),
                        p.Text,
                        p.Number,
                        p.Item.IsMarkedForReview,
                        p.Item.ClientSeq,
                        now),
                    p.Question.Answer.ClientSeq)).ToList();

                await db.SaveChangesAsync(ct);
                return new SaveAnswersResponse(now, attempt.ExpiredAt, saved);
            },
            ct);

        return outcome;
    }

    // ----- Sự kiện -----

    public async Task<Result<RecordEventsResponse>> RecordEventsAsync(
        Guid userId, Guid attemptId, RecordEventsRequest request, CancellationToken ct)
    {
        var errors = await eventsValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<RecordEventsResponse>.Failure(errors);
        }

        var attempt = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.Id == attemptId && a.UserId == userId)
            .Select(a => new { a.Status })
            .SingleOrDefaultAsync(ct);
        if (attempt is null)
        {
            return AttemptNotFound;
        }

        if (attempt.Status != AttemptStatus.InProgress)
        {
            return new RecordEventsResponse(0);
        }

        var existing = await db.AttemptEvents.CountAsync(e => e.AttemptId == attemptId, ct);
        var room = Math.Max(0, AttemptEvent.MaxEventsPerAttempt - existing);
        var now = Now;
        var accepted = request.Events.Take(room).ToList();
        db.AttemptEvents.AddRange(accepted.Select(e => new AttemptEvent(
            attemptId, e.Type, e.ClientTime is { } t ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null, now, currentUser.IpAddress, e.Detail)));
        await db.SaveChangesAsync(ct);
        return new RecordEventsResponse(accepted.Count);
    }

    // ----- Nộp bài (idempotent) -----

    public async Task<Result<StudentResultDto>> SubmitAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var outcome = await db.InTransactionAsync<Result<bool>>(
            async () =>
            {
                var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                if (attempt is null || attempt.UserId != userId)
                {
                    return AttemptNotFound;
                }

                if (attempt.IsInProgress)
                {
                    await finalizer.FinalizeLockedAsync(attempt, SubmitReason.Student, currentUser.IpAddress, ct);
                    await db.SaveChangesAsync(ct);
                }

                return true;
            },
            ct);

        if (outcome.IsFailure)
        {
            return Result<StudentResultDto>.Failure(outcome.Errors);
        }

        db.ChangeTracker.Clear();
        return await GetResultAsync(userId, attemptId, ct);
    }

    // ----- Kết quả và lịch sử -----

    public async Task<Result<StudentResultDto>> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
            .SingleOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);
        if (attempt is null)
        {
            return AttemptNotFound;
        }

        if (attempt.IsInProgress)
        {
            return Error.Business(ErrorCodes.ResultNotAvailable, "Lượt thi chưa được nộp.");
        }

        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        var version = await db.ExamVersions.AsNoTracking().SingleAsync(v => v.Id == attempt.ExamVersionId, ct);
        var result = await db.ExamResults.AsNoTracking().SingleOrDefaultAsync(r => r.AttemptId == attemptId, ct);
        var state = (await LoadUserExamStatesAsync(userId, [exam.Id], ct))[exam.Id];
        var visibility = ResultPolicy.Evaluate(
            attempt.Status, version.ScoreVisibility, version.ReviewPolicy, exam.EndAt, state.Used, state.Allowed, Now);
        // Còn câu tự luận chờ chấm tay: chưa công bố điểm và chưa cho xem lại (điểm đang là tạm tính)
        var pending = result is { PendingManualCount: > 0 };
        var show = visibility.ScoreVisible && result is not null && !pending;

        IReadOnlyList<ReviewQuestionDto>? review = null;
        if (visibility.ReviewAvailable && result is not null && !pending)
        {
            review = await BuildReviewAsync(attempt, ct);
        }

        return new StudentResultDto(
            attempt.Id,
            exam.Id,
            exam.Name,
            attempt.AttemptNumber,
            attempt.Status,
            attempt.SubmitReason,
            attempt.StartedAt,
            attempt.SubmittedAt,
            result?.DurationSeconds,
            show,
            show ? result!.TotalScore : null,
            show ? result!.MaxScore : null,
            show ? result!.Percentage : null,
            show ? result!.CorrectCount : null,
            attempt.QuestionCount,
            show ? result!.Passed : null,
            review is not null,
            visibility.ReviewAvailableAt,
            review,
            pending,
            // Chỉ ký ảnh của nội dung đang được trả về: chưa cho xem lại thì không có ảnh giải thích nào (D-27)
            review is null
                ? MediaLinks.EmptyMap
                : mediaLinks.For(review.SelectMany(q => (IEnumerable<string?>)[q.Content, q.Explanation, .. q.Options.Select(o => o.Content)])));
    }

    public async Task<PagedResult<StudentHistoryItemDto>> HistoryAsync(Guid userId, StudentHistoryQuery query, CancellationToken ct)
    {
        var attempts = db.ExamAttempts.AsNoTracking().Where(a => a.UserId == userId);
        var total = await attempts.CountAsync(ct);
        var rows = await attempts
            .OrderByDescending(a => a.StartedAt)
            .Skip(query.Skip).Take(query.PageSize)
            .Select(a => new
            {
                Attempt = a,
                Exam = db.Exams.Where(e => e.Id == a.ExamId).Select(e => new { e.Code, e.Name, e.EndAt }).Single(),
                Version = db.ExamVersions.Where(v => v.Id == a.ExamVersionId).Select(v => new { v.ScoreVisibility, v.ReviewPolicy }).Single(),
                Result = db.ExamResults.Where(r => r.AttemptId == a.Id)
                    .Select(r => new { r.TotalScore, r.MaxScore, r.Percentage, r.Passed, r.PendingManualCount }).SingleOrDefault(),
            })
            .ToListAsync(ct);

        var now = Now;
        var items = rows.Select(r =>
        {
            // Lịch sử chỉ cần biết có được xem điểm hay không; không cần số lượt còn lại.
            var pending = r.Result is { PendingManualCount: > 0 };
            var visible = ResultPolicy.Evaluate(
                r.Attempt.Status, r.Version.ScoreVisibility, r.Version.ReviewPolicy, r.Exam.EndAt, 0, int.MaxValue, now).ScoreVisible
                && r.Result is not null && !pending;
            return new StudentHistoryItemDto(
                r.Attempt.Id, r.Attempt.ExamId, r.Exam.Code, r.Exam.Name, r.Attempt.AttemptNumber, r.Attempt.Status,
                r.Attempt.StartedAt, r.Attempt.SubmittedAt, visible,
                visible ? r.Result!.TotalScore : null, visible ? r.Result!.MaxScore : null,
                visible ? r.Result!.Percentage : null, visible ? r.Result!.Passed : null, pending);
        }).ToList();

        return new PagedResult<StudentHistoryItemDto>(items, query.Page, query.PageSize, total);
    }

    // ----- Helpers -----

    /// <summary>
    /// Đề học viên được thấy: đã publish / đã đóng, và PUBLIC hoặc được gán (trực tiếp / qua nhóm đang hoạt động (D-10) /
    /// qua lớp học đang hoạt động mà học viên thuộc về (D-28)).
    /// </summary>
    private IQueryable<Exam> AccessibleExams(Guid userId)
    {
        var groupIds = db.UserGroupMembers
            .Where(m => m.UserId == userId && db.UserGroups.Any(g => g.Id == m.GroupId && g.IsActive))
            .Select(m => (Guid?)m.GroupId);
        var classroomIds = db.ClassroomStudents
            .Where(s => s.UserId == userId && db.Classrooms.Any(c => c.Id == s.ClassroomId && c.IsActive))
            .Select(s => (Guid?)s.ClassroomId);
        return db.Exams.AsNoTracking().Where(e =>
            e.Status != ExamStatus.Draft
            && (e.AccessMode == AccessMode.Public
                || e.Assignments.Any(a => a.UserId == userId || groupIds.Contains(a.GroupId) || classroomIds.Contains(a.ClassroomId))));
    }

    private sealed record UserExamState(
        ExamVersion Version, int Allowed, int Used, Guid? InProgressId, decimal? OfficialScore, ExamAvailability Availability);

    private async Task<Dictionary<Guid, UserExamState>> LoadUserExamStatesAsync(
        Guid userId, IReadOnlyCollection<Guid> examIds, CancellationToken ct)
    {
        var exams = await db.Exams.AsNoTracking().Where(e => examIds.Contains(e.Id)).ToListAsync(ct);
        var versions = await db.ExamVersions.AsNoTracking()
            .Where(v => examIds.Contains(v.ExamId) && v.Status == ExamVersionStatus.Published)
            .ToDictionaryAsync(v => v.ExamId, ct);
        var attempts = await db.ExamAttempts.AsNoTracking()
            .Where(a => examIds.Contains(a.ExamId) && a.UserId == userId)
            .Select(a => new { a.Id, a.ExamId, a.ExamVersionId, a.Status })
            .ToListAsync(ct);
        var extras = await db.ExamUserOverrides.AsNoTracking()
            .Where(o => examIds.Contains(o.ExamId) && o.UserId == userId)
            .ToDictionaryAsync(o => o.ExamId, o => o.ExtraAttempts, ct);
        var results = await db.ExamResults.AsNoTracking()
            .Where(r => examIds.Contains(r.ExamId) && r.UserId == userId
                && db.ExamAttempts.Any(a => a.Id == r.AttemptId && a.Status != AttemptStatus.Cancelled))
            .Select(r => new { r.ExamId, r.TotalScore, r.SubmittedAt, r.PendingManualCount })
            .ToListAsync(ct);

        var now = Now;
        var states = new Dictionary<Guid, UserExamState>();
        foreach (var exam in exams)
        {
            if (!versions.TryGetValue(exam.Id, out var version))
            {
                continue;
            }

            var mine = attempts.Where(a => a.ExamId == exam.Id).ToList();
            // Đếm số lượt đã làm trên phiên bản hiện tại đang được xuất bản (Publish)
            var used = mine.Count(a => a.ExamVersionId == version.Id && a.Status != AttemptStatus.Cancelled);
            var allowed = exam.MaxAttempts + extras.GetValueOrDefault(exam.Id);
            var inProgress = mine.FirstOrDefault(a => a.ExamVersionId == version.Id && a.Status == AttemptStatus.InProgress)?.Id;

            var availability = inProgress is not null ? ExamAvailability.InProgress
                : exam.Status == ExamStatus.Closed ? ExamAvailability.Closed
                : exam.EndAt is { } end && now >= end ? ExamAvailability.Ended
                : exam.StartAt is { } start && now < start ? ExamAvailability.NotStarted
                : used >= allowed ? ExamAvailability.NoAttemptsLeft
                : ExamAvailability.Available;

            // Điểm chính thức theo RetakeScoringPolicy (D-08), chỉ hiện khi chính sách cho xem điểm.
            // Kết quả còn câu tự luận chờ chấm chưa được tính vào điểm chính thức hiển thị cho học viên
            var examResults = results.Where(r => r.ExamId == exam.Id && r.PendingManualCount == 0).ToList();
            var scoreVisible = version.ScoreVisibility == ScoreVisibility.Immediate
                || (version.ScoreVisibility == ScoreVisibility.AfterExamEnd && exam.EndAt is { } e && e <= now);
            decimal? official = !scoreVisible || examResults.Count == 0 ? null
                : exam.RetakeScoringPolicy == RetakeScoringPolicy.Highest ? examResults.Max(r => r.TotalScore)
                : examResults.OrderByDescending(r => r.SubmittedAt).First().TotalScore;

            states[exam.Id] = new UserExamState(version, allowed, used, inProgress, official, availability);
        }

        return states;
    }

    private async Task<Result<AttemptDto>> GetAttemptAsync(Guid userId, Guid attemptId, bool resumed, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(a => a.Questions).ThenInclude(q => q.Answer).ThenInclude(a => a.SelectedOptions)
            .SingleOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);
        if (attempt is null)
        {
            return AttemptNotFound;
        }

        var examName = await db.Exams.Where(e => e.Id == attempt.ExamId).Select(e => e.Name).SingleAsync(ct);
        var snapshot = await db.ExamQuestions.AsNoTracking().Include(q => q.Options)
            .Where(q => q.ExamVersionId == attempt.ExamVersionId)
            .ToDictionaryAsync(q => q.Id, ct);

        var questions = attempt.Questions.OrderBy(q => q.QuestionOrder).Select(q =>
        {
            var player = ExamVersionService.ToPlayerQuestion(snapshot[q.ExamQuestionId], q.Id, q.QuestionOrder, q.OptionOrder);
            var answer = q.Answer;
            return new AttemptQuestionDto(
                player.Id,
                player.Order,
                player.Content,
                player.ContentFormat,
                player.Type,
                player.AnswerDataType,
                player.Score,
                player.Options,
                new AttemptAnswerStateDto(
                    answer.SelectedOptions.Select(o => o.OptionCode).Order(StringComparer.Ordinal).ToList(),
                    answer.AnswerText,
                    answer.IsMarkedForReview,
                    answer.ClientSeq));
        }).ToList();

        return new AttemptDto(
            attempt.Id, attempt.ExamId, examName, attempt.AttemptNumber, attempt.Status, resumed,
            attempt.StartedAt, attempt.ExpiredAt, Now, questions,
            mediaLinks.For(questions.SelectMany(q => (IEnumerable<string?>)[q.Content, .. q.Options.Select(o => o.Content)])));
    }

    /// <summary>Kiểm tra dạng câu trả lời theo loại câu (INVALID_OPTION / INVALID_ANSWER_SHAPE / INVALID_NUMBER_FORMAT).</summary>
    private static (Error? Error, string? Text, decimal? Number) ValidateShape(ExamQuestion question, SaveAnswerItem item, string field)
    {
        var codes = (item.SelectedOptions ?? []).Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        var text = string.IsNullOrWhiteSpace(item.AnswerText) ? null : item.AnswerText.Trim();

        if (question.QuestionType == QuestionType.Essay)
        {
            return codes.Count > 0
                ? (Error.Business(ErrorCodes.InvalidAnswerShape, "Câu tự luận không có lựa chọn.") with { Field = $"{field}.selectedOptions" }, null, null)
                : (null, text, null);
        }

        if (question.QuestionType == QuestionType.FillIn)
        {
            if (codes.Count > 0)
            {
                return (Error.Business(ErrorCodes.InvalidAnswerShape, "Câu điền không có lựa chọn.") with { Field = $"{field}.selectedOptions" }, null, null);
            }

            if (text is not null && question.AnswerDataType == AnswerDataType.Number)
            {
                return NumericAnswerParser.TryParse(text, out var number)
                    ? (null, text, number)
                    : (Error.Business(ErrorCodes.InvalidNumberFormat, "Số không hợp lệ. Ví dụ hợp lệ: 3,5 hoặc 3.5") with { Field = $"{field}.answerText" }, null, null);
            }

            return (null, text, null);
        }

        if (text is not null)
        {
            return (Error.Business(ErrorCodes.InvalidAnswerShape, "Câu trắc nghiệm không nhận câu trả lời dạng chữ.") with { Field = $"{field}.answerText" }, null, null);
        }

        if (question.QuestionType is QuestionType.SingleChoice or QuestionType.TrueFalse && codes.Count > 1)
        {
            return (Error.Business(ErrorCodes.InvalidAnswerShape, "Câu này chỉ được chọn một lựa chọn.") with { Field = $"{field}.selectedOptions" }, null, null);
        }

        var valid = question.Options.Select(o => o.OptionCode).ToHashSet(StringComparer.Ordinal);
        return codes.TrueForAll(valid.Contains)
            ? (null, null, null)
            : (Error.Business(ErrorCodes.InvalidOption, "Lựa chọn không thuộc câu hỏi.") with { Field = $"{field}.selectedOptions" }, null, null);
    }

    private async Task<IReadOnlyList<ReviewQuestionDto>> BuildReviewAsync(ExamAttempt attempt, CancellationToken ct)
    {
        var snapshot = await db.ExamQuestions.AsNoTracking()
            .Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .Where(q => q.ExamVersionId == attempt.ExamVersionId)
            .ToDictionaryAsync(q => q.Id, ct);

        return attempt.Questions.OrderBy(q => q.QuestionOrder).Select(q =>
        {
            var eq = snapshot[q.ExamQuestionId];
            var player = ExamVersionService.ToPlayerQuestion(eq, q.Id, q.QuestionOrder, q.OptionOrder);
            return new ReviewQuestionDto(
                q.Id,
                q.QuestionOrder,
                eq.Content,
                eq.ContentFormat,
                eq.QuestionType,
                eq.AnswerDataType,
                eq.Score,
                player.Options,
                q.Answer.SelectedOptions.Select(o => o.OptionCode).Order(StringComparer.Ordinal).ToList(),
                q.Answer.AnswerText,
                eq.Options.Where(o => o.IsCorrect).OrderBy(o => o.DisplayOrder).Select(o => o.OptionCode).ToList(),
                eq.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
                eq.CorrectAnswerNumber,
                q.Answer.IsCorrect ?? false,
                q.Answer.Score ?? 0,
                eq.IsVoided,
                eq.Explanation,
                q.Answer.ManualComment);
        }).ToList();
    }
}

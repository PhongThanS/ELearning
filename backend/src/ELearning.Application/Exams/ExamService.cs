using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Exams;

public interface IExamService
{
    Task<PagedResult<ExamListItemDto>> ListAsync(ExamListQuery query, CancellationToken ct);

    Task<Result<ExamDetailDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<ExamDetailDto>> CreateAsync(CreateExamRequest request, CancellationToken ct);

    Task<Result<ExamDetailDto>> UpdateAsync(Guid id, UpdateExamRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct);

    Task<Result<ExamDetailDto>> CloneAsync(Guid id, CloneExamRequest request, CancellationToken ct);

    Task<Result<ExamDetailDto>> CloseAsync(Guid id, CloseExamRequest request, CancellationToken ct);

    Task<Result<ExamDetailDto>> ReopenAsync(Guid id, CancellationToken ct);

    Task<Result<AssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct);

    Task<Result<AssignmentsDto>> SetAssignmentsAsync(Guid id, SetAssignmentsRequest request, CancellationToken ct);

    Task<Result<UserOverrideDto>> SetUserOverrideAsync(Guid id, Guid userId, SetUserOverrideRequest request, CancellationToken ct);
}

/// <summary>
/// Buộc nộp mọi lượt đang làm của một đề khi đóng đề với forceSubmitInProgress (D-06).
/// Được hiện thực ở module lượt thi (M5).
/// </summary>
public interface IExamAttemptCloser
{
    Task<int> ForceSubmitInProgressAsync(Guid examId, CancellationToken ct);
}

/// <summary>Đề thi (docs/02-nghiep-vu.md mục 4–5, docs/05-api.md mục 6.4).</summary>
internal sealed class ExamService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IEnumerable<IExamAttemptCloser> attemptClosers,
    IValidator<CreateExamRequest> createValidator,
    IValidator<UpdateExamRequest> updateValidator) : IExamService
{
    internal static readonly Error ExamNotFound = Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<ExamListItemDto>> ListAsync(ExamListQuery query, CancellationToken ct)
    {
        var exams = db.Exams.AsNoTracking();
        if (query.Status is { } status)
        {
            exams = exams.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            exams = exams.Where(e => EF.Functions.Like(e.Code, kw) || EF.Functions.Like(e.Name, kw));
        }

        exams = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", false) => exams.OrderBy(e => e.Code),
            ("code", true) => exams.OrderByDescending(e => e.Code),
            ("name", false) => exams.OrderBy(e => e.Name),
            ("name", true) => exams.OrderByDescending(e => e.Name),
            ("startat", false) => exams.OrderBy(e => e.StartAt),
            ("startat", true) => exams.OrderByDescending(e => e.StartAt),
            ("createdat", false) => exams.OrderBy(e => e.CreatedAt),
            _ => exams.OrderByDescending(e => e.CreatedAt),
        };

        var total = await exams.CountAsync(ct);
        var items = await exams.Skip(query.Skip).Take(query.PageSize)
            .Select(e => new ExamListItemDto(
                e.Id,
                e.Code,
                e.Name,
                e.Status,
                e.StartAt,
                e.EndAt,
                e.MaxAttempts,
                e.AccessMode,
                e.Versions.Where(v => v.Status == ExamVersionStatus.Published).Select(v => (int?)v.VersionNumber).FirstOrDefault(),
                e.Versions.Any(v => v.Status == ExamVersionStatus.Draft),
                db.ExamAttempts.Count(a => a.ExamId == e.Id),
                e.CreatedAt,
                e.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<ExamListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<ExamDetailDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        return exam is null ? ExamNotFound : await ToDetailAsync(exam, ct);
    }

    public async Task<Result<ExamDetailDto>> CreateAsync(CreateExamRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<ExamDetailDto>.Failure(errors);
        }

        var code = request.Code.Trim();
        if (await db.Exams.AnyAsync(e => e.Code == code, ct))
        {
            return Error.Conflict(ErrorCodes.DuplicateCode, "Mã đề đã tồn tại.");
        }

        var settings = new VersionSettings(request.DurationMinutes, request.PassPercentage, request.ScoreVisibility, request.ReviewPolicy);
        var exam = Exam.Create(code, request.ToDetails(), settings, currentUser.RequiredUserId, Now);
        db.Exams.Add(exam);
        audit.Write(AuditActions.ExamCreated, nameof(Exam), exam.Id, newValue: new { exam.Code, exam.Name });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(exam, ct);
    }

    public async Task<Result<ExamDetailDto>> UpdateAsync(Guid id, UpdateExamRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<ExamDetailDto>.Failure(errors);
        }

        var exam = await db.Exams.Include(e => e.Versions).Include(e => e.Assignments).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        if (!db.TryApplyRowVersion(exam, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        var details = request.ToDetails();
        if (details.RetakeScoringPolicy != exam.RetakeScoringPolicy && await db.ExamAttempts.AnyAsync(a => a.ExamId == id, ct))
        {
            return Error.Conflict(ErrorCodes.RetakePolicyLocked, "Không thể đổi cách tính điểm khi đề đã có lượt thi.");
        }

        if (!string.IsNullOrWhiteSpace(request.Code) && request.Code.Trim() != exam.Code)
        {
            if (exam.Status != ExamStatus.Draft)
            {
                return Error.Conflict(ErrorCodes.ExamNotDraft, "Chỉ đổi được mã khi đề chưa từng publish.");
            }

            var code = request.Code.Trim();
            if (await db.Exams.AnyAsync(e => e.Code == code && e.Id != id, ct))
            {
                return Error.Conflict(ErrorCodes.DuplicateCode, "Mã đề đã tồn tại.");
            }

            exam.ChangeCode(code);
        }

        // Quy tắc chéo với version đang publish (docs/02-nghiep-vu.md mục 4.5)
        if (exam.Versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Published) is { } published)
        {
            var issues = CrossRuleIssues(details, published.Settings, exam.Assignments.Count > 0);
            if (issues.Count > 0)
            {
                return Result<ExamDetailDto>.Failure(issues);
            }
        }

        var old = Snapshot(exam);
        exam.UpdateDetails(details, currentUser.RequiredUserId, Now);
        audit.Write(AuditActions.ExamUpdated, nameof(Exam), exam.Id, old, Snapshot(exam));
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(exam, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var exam = await db.Exams
            .Include(e => e.Assignments)
            .Include(e => e.Versions).ThenInclude(v => v.Questions).ThenInclude(q => q.Options)
            .Include(e => e.Versions).ThenInclude(v => v.Questions).ThenInclude(q => q.AcceptedAnswers)
            .SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Error.Conflict(ErrorCodes.ExamNotDraft, "Chỉ xóa được đề chưa từng publish.");
        }

        db.ExamUserOverrides.RemoveRange(await db.ExamUserOverrides.Where(o => o.ExamId == id).ToListAsync(ct));
        db.Exams.Remove(exam);
        audit.Write(AuditActions.ExamDeleted, nameof(Exam), exam.Id, new { exam.Code, exam.Name });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ExamDetailDto>> CloneAsync(Guid id, CloneExamRequest request, CancellationToken ct)
    {
        var source = await db.Exams.AsNoTracking()
            .Include(e => e.Versions).ThenInclude(v => v.Questions).ThenInclude(q => q.Options)
            .Include(e => e.Versions).ThenInclude(v => v.Questions).ThenInclude(q => q.AcceptedAnswers)
            .SingleOrDefaultAsync(e => e.Id == id, ct);
        if (source is null)
        {
            return ExamNotFound;
        }

        string code;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            code = request.Code.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, ExamRules.CodePattern))
            {
                return Error.Validation("CODE_INVALID", "Mã đề không hợp lệ.", "code");
            }

            if (await db.Exams.AnyAsync(e => e.Code == code, ct))
            {
                return Error.Conflict(ErrorCodes.DuplicateCode, "Mã đề đã tồn tại.");
            }
        }
        else
        {
            code = await NextCopyCodeAsync(source.Code, ct);
        }

        // Nguồn: version đang publish, nếu không có thì version mới nhất
        var sourceVersion = source.Versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Published)
            ?? source.Versions.OrderByDescending(v => v.VersionNumber).First();

        var details = new ExamDetails(
            string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name} (bản sao)" : request.Name.Trim(),
            source.Description,
            source.Instructions,
            null,
            null,
            source.MaxAttempts,
            source.AccessMode,
            source.RetakeScoringPolicy);
        var clone = Exam.Create(code, details, sourceVersion.Settings, currentUser.RequiredUserId, Now);
        clone.Versions.Single().CopyFrom(sourceVersion, Now);
        db.Exams.Add(clone);
        audit.Write(AuditActions.ExamCreated, nameof(Exam), clone.Id, newValue: new { clone.Code, ClonedFrom = source.Code });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(clone, ct);
    }

    public async Task<Result<ExamDetailDto>> CloseAsync(Guid id, CloseExamRequest request, CancellationToken ct)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        exam.Close(currentUser.RequiredUserId, Now);
        audit.Write(AuditActions.ExamClosed, nameof(Exam), exam.Id, newValue: new { request.ForceSubmitInProgress });
        await db.SaveChangesAsync(ct);

        if (request.ForceSubmitInProgress)
        {
            foreach (var closer in attemptClosers)
            {
                await closer.ForceSubmitInProgressAsync(id, ct);
            }
        }

        return await ToDetailAsync(exam, ct);
    }

    public async Task<Result<ExamDetailDto>> ReopenAsync(Guid id, CancellationToken ct)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        exam.Reopen(currentUser.RequiredUserId, Now);
        audit.Write(AuditActions.ExamReopened, nameof(Exam), exam.Id);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(exam, ct);
    }

    public async Task<Result<AssignmentsDto>> GetAssignmentsAsync(Guid id, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().Include(e => e.Assignments).SingleOrDefaultAsync(e => e.Id == id, ct);
        return exam is null ? ExamNotFound : await ToAssignmentsAsync(exam, ct);
    }

    public async Task<Result<AssignmentsDto>> SetAssignmentsAsync(Guid id, SetAssignmentsRequest request, CancellationToken ct)
    {
        var groupIds = request.GroupIds?.Distinct().ToList() ?? [];
        var userIds = request.UserIds?.Distinct().ToList() ?? [];
        if (groupIds.Count + userIds.Count > 1000)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Tối đa 1000 nhóm và người được gán.", "userIds");
        }

        var exam = await db.Exams.Include(e => e.Assignments).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null)
        {
            return ExamNotFound;
        }

        if (await db.UserGroups.CountAsync(g => groupIds.Contains(g.Id), ct) != groupIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có nhóm không tồn tại.", "groupIds");
        }

        if (await db.Users.CountAsync(u => userIds.Contains(u.Id), ct) != userIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có người dùng không tồn tại.", "userIds");
        }

        if (exam.Status != ExamStatus.Draft && exam.AccessMode == AccessMode.Assigned && groupIds.Count + userIds.Count == 0)
        {
            return Error.Business("NO_ASSIGNMENTS", "Đề đã publish ở chế độ giới hạn người thi phải được gán cho ít nhất một nhóm hoặc người.");
        }

        if (exam.SetAssignments(groupIds, userIds, currentUser.RequiredUserId, Now))
        {
            audit.Write(AuditActions.ExamAssignmentsChanged, nameof(Exam), exam.Id, newValue: new { GroupIds = groupIds, UserIds = userIds });
            await db.SaveChangesAsync(ct);
        }

        return await ToAssignmentsAsync(exam, ct);
    }

    public async Task<Result<UserOverrideDto>> SetUserOverrideAsync(
        Guid id, Guid userId, SetUserOverrideRequest request, CancellationToken ct)
    {
        if (request.ExtraAttempts is < 0 or > Exam.MaxAttemptsLimit)
        {
            return Error.Validation("EXTRA_ATTEMPTS_INVALID", $"Số lượt cấp thêm phải từ 0 đến {Exam.MaxAttemptsLimit}.", "extraAttempts");
        }

        if (!await db.Exams.AnyAsync(e => e.Id == id, ct))
        {
            return ExamNotFound;
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            return Error.NotFound(message: "Không tìm thấy người dùng.");
        }

        var entry = await db.ExamUserOverrides.SingleOrDefaultAsync(o => o.ExamId == id && o.UserId == userId, ct);
        var old = entry?.ExtraAttempts ?? 0;
        if (entry is null)
        {
            entry = new ExamUserOverride(id, userId, request.ExtraAttempts, request.Note, currentUser.RequiredUserId, Now);
            db.ExamUserOverrides.Add(entry);
        }
        else
        {
            entry.Set(request.ExtraAttempts, request.Note, currentUser.RequiredUserId, Now);
        }

        audit.Write(
            AuditActions.AttemptsGranted,
            nameof(Exam),
            id,
            new { UserId = userId, ExtraAttempts = old },
            new { UserId = userId, request.ExtraAttempts },
            request.Note);
        await db.SaveChangesAsync(ct);
        return new UserOverrideDto(entry.ExamId, entry.UserId, entry.ExtraAttempts, entry.Note, entry.UpdatedAt);
    }

    internal static List<Error> CrossRuleIssues(ExamDetails details, VersionSettings settings, bool hasAssignments)
    {
        var issues = new List<Error>();
        if (settings.ReviewPolicy == ReviewPolicy.AfterSubmit && details.MaxAttempts > 1)
        {
            issues.Add(Error.Business(
                "REVIEW_AFTER_SUBMIT_WITH_RETAKES", "Phiên bản đang publish cho xem đáp án ngay sau khi nộp nên đề chỉ được 1 lượt thi."));
        }

        if (details.EndAt is null
            && (settings.ReviewPolicy == ReviewPolicy.AfterExamEnd || settings.ScoreVisibility == ScoreVisibility.AfterExamEnd))
        {
            issues.Add(Error.Business("END_AT_REQUIRED", "Phiên bản đang publish cần có thời điểm kết thúc."));
        }

        if (details.AccessMode == AccessMode.Assigned && !hasAssignments)
        {
            issues.Add(Error.Business("NO_ASSIGNMENTS", "Đề giới hạn người thi nhưng chưa gán cho nhóm hoặc người nào."));
        }

        return issues;
    }

    private async Task<string> NextCopyCodeAsync(string sourceCode, CancellationToken ct)
    {
        var baseCode = sourceCode.Length > 90 ? sourceCode[..90] : sourceCode;
        for (var i = 1; ; i++)
        {
            var candidate = i == 1 ? $"{baseCode}-COPY" : $"{baseCode}-COPY{i}";
            if (!await db.Exams.AnyAsync(e => e.Code == candidate, ct))
            {
                return candidate;
            }
        }
    }

    private static object Snapshot(Exam e) => new
    {
        e.Code,
        e.Name,
        e.StartAt,
        e.EndAt,
        e.MaxAttempts,
        e.AccessMode,
        e.RetakeScoringPolicy,
    };

    private async Task<AssignmentsDto> ToAssignmentsAsync(Exam exam, CancellationToken ct)
    {
        var groupIds = exam.Assignments.Where(a => a.GroupId != null).Select(a => a.GroupId!.Value).ToList();
        var userIds = exam.Assignments.Where(a => a.UserId != null).Select(a => a.UserId!.Value).ToList();
        var groups = await db.UserGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id))
            .OrderBy(g => g.Code)
            .Select(g => new AssignedGroupDto(g.Id, g.Code, g.Name, g.Members.Count)).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.UserName)
            .Select(u => new AssignedUserDto(u.Id, u.UserName, u.FullName)).ToListAsync(ct);
        return new AssignmentsDto(exam.AccessMode, groups, users);
    }

    internal async Task<ExamDetailDto> ToDetailAsync(Exam exam, CancellationToken ct)
    {
        var versions = await db.ExamVersions.AsNoTracking().Where(v => v.ExamId == exam.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new VersionSummaryDto(
                v.Id,
                v.VersionNumber,
                v.Status,
                v.Questions.Count,
                v.Questions.Sum(q => (decimal?)q.Score) ?? 0,
                v.PublishedAt,
                v.ArchivedAt,
                v.CreatedAt))
            .ToListAsync(ct);
        var assignmentCount = await db.ExamAssignments.CountAsync(a => a.ExamId == exam.Id, ct);
        var hasAttempts = await db.ExamAttempts.AnyAsync(a => a.ExamId == exam.Id, ct);

        return new ExamDetailDto(
            exam.Id,
            exam.Code,
            exam.Name,
            exam.Description,
            exam.Instructions,
            exam.Status,
            exam.StartAt,
            exam.EndAt,
            exam.MaxAttempts,
            exam.AccessMode,
            exam.RetakeScoringPolicy,
            versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Published)?.Id,
            versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Draft)?.Id,
            assignmentCount,
            hasAttempts,
            versions,
            exam.CreatedAt,
            exam.UpdatedAt,
            exam.ClosedAt,
            exam.RowVersion.ToBase64());
    }
}

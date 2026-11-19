using System.Text.Json;
using ELearning.Application.Admin;
using ELearning.Application.Attendance;
using ELearning.Application.Audit;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Classes;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Domain.Zalo;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Zalo;

/// <summary>
/// Gửi tin Zalo cho phụ huynh cả lớp (D-45, docs/02-nghiep-vu.md mục 5.3). Tạo đợt gửi + từng tin trong hàng đợi;
/// job nền <see cref="IZaloDispatchService"/> gửi dần. Phạm vi lớp giống điểm danh (chủ nhiệm hoặc Attendance.ManageAll);
/// lớp ngoài phạm vi → 404.
/// </summary>
internal sealed class ZaloBroadcastService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    IUserAccessService userAccess,
    ZaloMessageComposer composer,
    IOptions<ZaloOptions> zaloOptions,
    IOptions<AppOptions> appOptions,
    TimeProvider time) : IZaloBroadcastService
{
    public const int MaxListed = 30;

    private static readonly Error ClassroomNotFound = Error.NotFound(ErrorCodes.ClassroomNotFound, "Không tìm thấy lớp học.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private sealed record Candidate(
        Guid StudentId, string StudentName, string? ParentName, string? ParentPhone, string? Phone,
        AttendanceStatus? Status, decimal? Score, string? Note);

    private sealed record Plan(Classroom Classroom, List<ZaloRecipientDto> Recipients, List<(Guid StudentId, string? ParentName, string Phone, string TemplateId, Dictionary<string, string> Data)> Sendable);

    public ZaloStatusDto GetStatus()
    {
        var options = zaloOptions.Value;
        var missing = new List<string>();
        if (options.IsZns)
        {
            foreach (var kind in Enum.GetValues<ZaloMessageKind>())
            {
                if (composer.TemplateId(kind) is null)
                {
                    missing.Add(ZaloMessageComposer.TemplateKey(kind));
                }
            }
        }

        return new ZaloStatusDto(options.IsEnabled, options.Provider, !string.IsNullOrWhiteSpace(options.Mode), missing);
    }

    public async Task<Result<ZaloPreviewDto>> PreviewAsync(Guid classroomId, ZaloBroadcastRequest request, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var plan = await BuildPlanAsync(classroom, request, ct);
        if (plan.IsFailure)
        {
            // Buổi chưa điểm danh: vẫn cho xem trước (danh sách trống) để giao diện giải thích
            return plan.FirstError.Code == ErrorCodes.ZaloNoRecipients
                ? new ZaloPreviewDto(request.Kind, 0, false, [])
                : plan.FirstError;
        }

        var alreadySent = await AlreadySentAsync(classroomId, request, ct);
        return new ZaloPreviewDto(request.Kind, plan.Value.Sendable.Count, alreadySent, plan.Value.Recipients);
    }

    public async Task<Result<ZaloBroadcastCreatedDto>> CreateAsync(Guid classroomId, ZaloBroadcastRequest request, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (!zaloOptions.Value.IsEnabled)
        {
            return Error.Business(ErrorCodes.ZaloNotConfigured, "Chưa bật gửi tin Zalo. Liên hệ quản trị viên để cấu hình.");
        }

        if (zaloOptions.Value.IsZns && composer.TemplateId(request.Kind) is null)
        {
            return Error.Business(ErrorCodes.ZaloNotConfigured, "Chưa cấu hình mẫu tin Zalo (ZNS) cho loại tin này.");
        }

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        if (!request.Force && await AlreadySentAsync(classroomId, request, ct))
        {
            return Error.Conflict(ErrorCodes.ZaloAlreadySent, "Báo cáo buổi học này đã được gửi cho phụ huynh. Gửi lại sẽ khiến phụ huynh nhận tin lần nữa.");
        }

        var planResult = await BuildPlanAsync(classroom, request, ct);
        if (planResult.IsFailure)
        {
            return planResult.FirstError;
        }

        var plan = planResult.Value;
        if (plan.Sendable.Count == 0)
        {
            return Error.Business(ErrorCodes.ZaloNoRecipients, "Không có phụ huynh nào đủ điều kiện nhận tin (thiếu số điện thoại hoặc chưa điểm danh).");
        }

        var userId = currentUser.RequiredUserId;
        var now = Now;
        var broadcast = new ZaloBroadcast(
            classroomId, request.Kind, request.Kind == ZaloMessageKind.DailyReport ? request.SessionDate : null,
            request.Kind == ZaloMessageKind.Announcement ? request.Content!.Trim() : null, userId, now);
        db.ZaloBroadcasts.Add(broadcast);
        foreach (var item in plan.Sendable)
        {
            broadcast.AddMessage(item.StudentId, item.ParentName, item.Phone, item.TemplateId, JsonSerializer.Serialize(item.Data), now);
        }

        // Không ghi số điện thoại / nội dung vào audit (dữ liệu cá nhân); chỉ ghi số lượng
        audit.Write(
            AuditActions.ZaloBroadcastCreated, nameof(ZaloBroadcast), broadcast.Id, null,
            new { classroom.Code, request.Kind, broadcast.SessionDate, Recipients = plan.Sendable.Count, Skipped = plan.Recipients.Count - plan.Sendable.Count, request.Force });
        await db.SaveChangesAsync(ct);

        var dto = await ToDtoAsync(broadcast.Id, ct);
        return new ZaloBroadcastCreatedDto(dto!, plan.Recipients.Where(r => r.SkipReason is not null).ToList());
    }

    public async Task<Result<IReadOnlyList<ZaloBroadcastDto>>> ListAsync(Guid classroomId, CancellationToken ct)
    {
        if (await FindClassAsync(classroomId, ct) is null)
        {
            return ClassroomNotFound;
        }

        var ids = await db.ZaloBroadcasts.AsNoTracking().Where(b => b.ClassroomId == classroomId)
            .OrderByDescending(b => b.CreatedAt).Select(b => b.Id).Take(MaxListed).ToListAsync(ct);
        var items = await ToDtosAsync(ids, ct);
        return Result<IReadOnlyList<ZaloBroadcastDto>>.Success(items);
    }

    public async Task<Result<ZaloBroadcastDetailDto>> GetAsync(Guid classroomId, Guid broadcastId, CancellationToken ct)
    {
        if (await FindClassAsync(classroomId, ct) is null || !await BelongsAsync(classroomId, broadcastId, ct))
        {
            return ClassroomNotFound;
        }

        var messages = await (
                from m in db.ZaloMessages.AsNoTracking()
                where m.BroadcastId == broadcastId
                join u in db.Users on m.StudentId equals u.Id
                orderby u.FullName
                select new ZaloMessageDto(m.Id, m.StudentId, u.FullName, m.RecipientName, m.Phone, m.Status, m.Attempts, m.SentAt, m.ErrorCode, m.ErrorMessage))
            .Take(ClassroomMax)
            .ToListAsync(ct);
        return new ZaloBroadcastDetailDto((await ToDtoAsync(broadcastId, ct))!, messages);
    }

    public async Task<Result<ZaloBroadcastDto>> RetryFailedAsync(Guid classroomId, Guid broadcastId, CancellationToken ct)
    {
        if (await FindClassAsync(classroomId, ct) is null || !await BelongsAsync(classroomId, broadcastId, ct))
        {
            return ClassroomNotFound;
        }

        if (!zaloOptions.Value.IsEnabled)
        {
            return Error.Business(ErrorCodes.ZaloNotConfigured, "Chưa bật gửi tin Zalo. Liên hệ quản trị viên để cấu hình.");
        }

        var failed = await db.ZaloMessages.Where(m => m.BroadcastId == broadcastId && m.Status == ZaloMessageStatus.Failed).ToListAsync(ct);
        if (failed.Count == 0)
        {
            return Error.Business(ErrorCodes.ZaloNoRecipients, "Đợt này không có tin lỗi để gửi lại.");
        }

        failed.ForEach(m => m.Requeue());
        audit.Write(AuditActions.ZaloBroadcastRetried, nameof(ZaloBroadcast), broadcastId, null, new { Requeued = failed.Count });
        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(broadcastId, ct))!;
    }

    public async Task<Result<ZaloBroadcastDto>> CancelAsync(Guid classroomId, Guid broadcastId, CancellationToken ct)
    {
        if (await FindClassAsync(classroomId, ct) is null || !await BelongsAsync(classroomId, broadcastId, ct))
        {
            return ClassroomNotFound;
        }

        var broadcast = await db.ZaloBroadcasts.Include(b => b.Messages).SingleAsync(b => b.Id == broadcastId, ct);
        var cancelled = broadcast.CancelPending(Now);
        if (cancelled == 0)
        {
            return Error.Business(ErrorCodes.ZaloNoRecipients, "Đợt này không còn tin nào đang chờ gửi.");
        }

        audit.Write(AuditActions.ZaloBroadcastCancelled, nameof(ZaloBroadcast), broadcastId, null, new { Cancelled = cancelled });
        // Status là concurrency token: job vừa nhận tin để gửi thì lưu thất bại → 409, người dùng thử lại
        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(broadcastId, ct))!;
    }

    private const int ClassroomMax = ELearning.Application.Classes.ClassroomService.MaxStudentsPerRequest;

    // ---------------------------------------------------------------- lập danh sách người nhận

    private Error? Validate(ZaloBroadcastRequest request)
    {
        if (!Enum.IsDefined(request.Kind))
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Loại tin không hợp lệ.", "kind");
        }

        if (request.Kind == ZaloMessageKind.DailyReport)
        {
            if (request.SessionDate is not { } date || date.Year < 2000)
            {
                return Error.Validation(ErrorCodes.ValidationFailed, "Cần chọn ngày buổi học.", "sessionDate");
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Cần nhập nội dung thông báo.", "content");
        }

        var max = Math.Min(ZaloBroadcast.MaxTextLength, zaloOptions.Value.MaxParamLength);
        if (request.Content?.Trim().Length > max)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, $"Nội dung tối đa {max} ký tự.", "content");
        }

        foreach (var (value, field) in new[] { (request.Deadline, "deadline"), (request.Attention, "attention") })
        {
            if (value?.Trim().Length > zaloOptions.Value.MaxParamLength)
            {
                return Error.Validation(ErrorCodes.ValidationFailed, $"Tối đa {zaloOptions.Value.MaxParamLength} ký tự.", field);
            }
        }

        if (request.StudentIds?.Count > ClassroomMax)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Chọn quá nhiều học sinh.", "studentIds");
        }

        return null;
    }

    private async Task<Result<Plan>> BuildPlanAsync(Classroom classroom, ZaloBroadcastRequest request, CancellationToken ct)
    {
        var students = await (
                from cs in db.ClassroomStudents.AsNoTracking()
                where cs.ClassroomId == classroom.Id
                join u in db.Users on cs.UserId equals u.Id
                join c in db.StudentContacts on u.Id equals c.UserId into contacts
                from c in contacts.DefaultIfEmpty()
                orderby u.FullName
                select new { u.Id, u.FullName, ParentName = c != null ? c.ParentName : null, ParentPhone = c != null ? c.ParentPhone : null })
            .Take(ClassroomMax)
            .ToListAsync(ct);

        var marks = new Dictionary<Guid, (AttendanceStatus? Status, decimal? Score, string? Note)>();
        string? topic = null, homework = null;
        if (request.Kind == ZaloMessageKind.DailyReport)
        {
            var session = await db.AttendanceSessions.AsNoTracking().Include(s => s.Records)
                .SingleOrDefaultAsync(s => s.ClassroomId == classroom.Id && s.SessionDate == request.SessionDate, ct);
            if (session is null)
            {
                return Error.Business(ErrorCodes.ZaloNoRecipients, "Buổi học này chưa được điểm danh.");
            }

            (topic, homework) = (session.Topic, session.NextHomework);
            marks = session.Records.ToDictionary(r => r.StudentId, r => (r.Status, r.HomeworkScore, r.Note));
        }

        var selected = request.StudentIds is { Count: > 0 } ? request.StudentIds.ToHashSet() : null;
        var recipients = new List<ZaloRecipientDto>();
        var sendable = new List<(Guid, string?, string, string, Dictionary<string, string>)>();
        var seenPhones = new HashSet<string>();
        var date = request.Kind == ZaloMessageKind.DailyReport ? request.SessionDate!.Value : Today();

        foreach (var s in students)
        {
            marks.TryGetValue(s.Id, out var mark);
            var candidate = new Candidate(s.Id, s.FullName, s.ParentName, s.ParentPhone, ZaloPhone.Normalize(s.ParentPhone), mark.Status, mark.Score, mark.Note);

            string? skip = null;
            if (selected is not null && !selected.Contains(s.Id))
            {
                skip = "NOT_SELECTED";
            }
            else if (string.IsNullOrWhiteSpace(s.ParentPhone))
            {
                skip = "NO_PHONE";
            }
            else if (candidate.Phone is null)
            {
                skip = "INVALID_PHONE";
            }
            else if (request.Kind == ZaloMessageKind.DailyReport && candidate.Status is null)
            {
                skip = "NOT_MARKED";
            }
            else if (request.Kind == ZaloMessageKind.Announcement && !seenPhones.Add(candidate.Phone))
            {
                // Anh chị em cùng lớp dùng chung số: một thông báo chung chỉ cần gửi một lần
                skip = "DUPLICATE_PHONE";
            }

            if (skip is not null)
            {
                recipients.Add(new ZaloRecipientDto(s.Id, s.FullName, s.ParentName, s.ParentPhone, skip, null));
                continue;
            }

            var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ZaloFields.ParentName] = string.IsNullOrWhiteSpace(s.ParentName) ? ZaloMessageComposer.DefaultParentName : s.ParentName,
                [ZaloFields.StudentName] = s.FullName,
                [ZaloFields.ClassName] = classroom.Name,
                [ZaloFields.Date] = ZaloMessageComposer.DateText(date),
                [ZaloFields.Attendance] = ZaloMessageComposer.AttendanceLabel(candidate.Status),
                [ZaloFields.Score] = ZaloMessageComposer.ScoreText(candidate.Score),
                [ZaloFields.Topic] = topic,
                [ZaloFields.Homework] = homework,
                [ZaloFields.Deadline] = request.Deadline,
                [ZaloFields.Attention] = request.Attention,
                [ZaloFields.Note] = candidate.Note,
                [ZaloFields.Content] = request.Content,
            };
            var (templateId, data) = composer.Compose(request.Kind, fields);
            recipients.Add(new ZaloRecipientDto(s.Id, s.FullName, s.ParentName, s.ParentPhone, null, data));
            sendable.Add((s.Id, s.ParentName, candidate.Phone!, templateId, data));
        }

        return new Plan(classroom, recipients, sendable);
    }

    private Task<bool> AlreadySentAsync(Guid classroomId, ZaloBroadcastRequest request, CancellationToken ct) =>
        request.Kind != ZaloMessageKind.DailyReport
            ? Task.FromResult(false)
            : db.ZaloBroadcasts.AnyAsync(
                b => b.ClassroomId == classroomId && b.Kind == ZaloMessageKind.DailyReport && b.SessionDate == request.SessionDate
                    && b.Messages.Any(m => m.Status != ZaloMessageStatus.Cancelled),
                ct);

    // ---------------------------------------------------------------- truy vấn

    private async Task<bool> BelongsAsync(Guid classroomId, Guid broadcastId, CancellationToken ct) =>
        await db.ZaloBroadcasts.AnyAsync(b => b.Id == broadcastId && b.ClassroomId == classroomId, ct);

    private async Task<ZaloBroadcastDto?> ToDtoAsync(Guid id, CancellationToken ct) =>
        (await ToDtosAsync([id], ct)).FirstOrDefault();

    private async Task<List<ZaloBroadcastDto>> ToDtosAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var rows = await (
                from b in db.ZaloBroadcasts.AsNoTracking()
                where ids.Contains(b.Id)
                select new
                {
                    b.Id,
                    b.Kind,
                    b.SessionDate,
                    b.Content,
                    b.CreatedAt,
                    CreatedByName = db.Users.Where(u => u.Id == b.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                    Total = b.Messages.Count,
                    Pending = b.Messages.Count(m => m.Status == ZaloMessageStatus.Pending || m.Status == ZaloMessageStatus.Sending),
                    Sent = b.Messages.Count(m => m.Status == ZaloMessageStatus.Sent),
                    Failed = b.Messages.Count(m => m.Status == ZaloMessageStatus.Failed),
                    Cancelled = b.Messages.Count(m => m.Status == ZaloMessageStatus.Cancelled),
                })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.CreatedAt)
            .Select(r => new ZaloBroadcastDto(r.Id, r.Kind, r.SessionDate, r.Content, r.CreatedAt, r.CreatedByName, r.Total, r.Pending, r.Sent, r.Failed, r.Cancelled))
            .ToList();
    }

    // ---------------------------------------------------------------- phạm vi lớp (giống điểm danh)

    private async Task<Classroom?> FindClassAsync(Guid classroomId, CancellationToken ct)
    {
        var classes = db.Classrooms.AsNoTracking();
        var canAll = (await userAccess.GetAsync(currentUser.RequiredUserId, ct))?.Permissions.Contains(Permissions.AttendanceManageAll) == true;
        if (!canAll)
        {
            var userId = currentUser.RequiredUserId;
            classes = classes.Where(c => c.HomeroomTeacherId == userId);
        }

        return await classes.SingleOrDefaultAsync(c => c.Id == classroomId, ct);
    }

    private DateOnly Today()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(appOptions.Value.BusinessTimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, zone));
    }
}

using System.Globalization;
using ELearning.Application.Admin;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Attendance;
using ELearning.Domain.Classes;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Attendance;

/// <summary>Lớp hiển thị ở ô chọn lớp của trang điểm danh.</summary>
public sealed record AttendanceClassDto(
    Guid Id, string Code, string Name, string? SchoolYear, bool IsActive, int StudentCount, string? HomeroomTeacherName);

public sealed record AttendanceClassQuery
{
    public string? Keyword { get; init; }
}

public sealed record AttendanceStudentDto(
    Guid StudentId,
    string UserName,
    string FullName,
    string? ParentName,
    string? ParentPhone,
    AttendanceStatus? Status,
    HomeworkStatus? HomeworkStatus,
    decimal? HomeworkScore,
    string? Note,
    bool InClass);

/// <summary>Buổi học của lớp trong một ngày. <c>Saved = false</c>: chưa lưu lần nào (mọi học sinh chưa điểm danh).</summary>
public sealed record AttendanceSessionDto(
    Guid ClassroomId,
    string ClassroomCode,
    string ClassroomName,
    DateOnly SessionDate,
    bool Saved,
    string? Topic,
    string? NextHomework,
    DateTime? UpdatedAt,
    string? UpdatedByName,
    IReadOnlyList<AttendanceStudentDto> Students);

public sealed record AttendanceRecordInput(
    Guid StudentId, AttendanceStatus? Status, HomeworkStatus? HomeworkStatus, decimal? HomeworkScore, string? Note);

/// <summary>Lưu tự động: chủ đề + BTVN luôn được ghi; <c>Records</c> chỉ cần các học sinh vừa đổi.</summary>
public sealed record SaveAttendanceRequest(string? Topic, string? NextHomework, IReadOnlyList<AttendanceRecordInput>? Records);

public sealed record SaveAttendanceResultDto(DateTime UpdatedAt, int SavedRecords);

public sealed record UpdateStudentContactRequest(string? ParentName, string? ParentPhone);

public sealed record StudentContactDto(Guid StudentId, string? ParentName, string? ParentPhone);

public sealed record AttendanceHistoryItemDto(
    DateOnly SessionDate, string? Topic, AttendanceStatus? Status, HomeworkStatus? HomeworkStatus, decimal? HomeworkScore, string? Note);

/// <summary>Lịch sử một tháng của học sinh trong lớp: mỗi buổi của lớp một dòng, kèm thứ hạng điểm TB trong lớp.</summary>
public sealed record AttendanceStudentHistoryDto(
    Guid StudentId,
    string UserName,
    string FullName,
    string? ParentName,
    string? ParentPhone,
    string ClassroomName,
    int Year,
    int Month,
    decimal? AverageScore,
    int? ClassRank,
    int RankedCount,
    IReadOnlyList<AttendanceHistoryItemDto> Items);

public sealed record AttendanceHistoryQuery
{
    /// <summary>Tháng dạng yyyy-MM; bỏ trống = tháng hiện tại theo múi giờ nghiệp vụ.</summary>
    public string? Month { get; init; }
}

public interface IAttendanceExporter
{
    byte[] Export(AttendanceSessionDto session, AttendanceMonthSheet month);
}

/// <summary>Bảng tổng hợp tháng cho file Excel: mỗi học sinh một dòng, mỗi buổi một cột.</summary>
public sealed record AttendanceMonthSheet(int Year, int Month, IReadOnlyList<DateOnly> Dates, IReadOnlyList<AttendanceMonthRow> Rows);

public sealed record AttendanceMonthRow(string UserName, string FullName, IReadOnlyList<AttendanceHistoryItemDto?> Cells);

public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceClassDto>> ListClassesAsync(AttendanceClassQuery query, CancellationToken ct);

    Task<Result<AttendanceSessionDto>> GetSessionAsync(Guid classroomId, DateOnly date, CancellationToken ct);

    Task<Result<SaveAttendanceResultDto>> SaveSessionAsync(Guid classroomId, DateOnly date, SaveAttendanceRequest request, CancellationToken ct);

    Task<Result<AttendanceSessionDto>> ResetSessionAsync(Guid classroomId, DateOnly date, CancellationToken ct);

    Task<Result<StudentContactDto>> UpdateContactAsync(Guid classroomId, Guid studentId, UpdateStudentContactRequest request, CancellationToken ct);

    Task<Result<AttendanceStudentHistoryDto>> GetStudentHistoryAsync(Guid classroomId, Guid studentId, AttendanceHistoryQuery query, CancellationToken ct);

    /// <summary>Học sinh đang đăng nhập xem lịch sử tháng của chính mình trong một lớp mình học; lớp khác → 404.</summary>
    Task<Result<AttendanceStudentHistoryDto>> GetMyHistoryAsync(Guid classroomId, AttendanceHistoryQuery query, CancellationToken ct);

    Task<Result<ExportFile>> ExportAsync(Guid classroomId, DateOnly date, CancellationToken ct);
}

internal sealed class SaveAttendanceRequestValidator : AbstractValidator<SaveAttendanceRequest>
{
    public SaveAttendanceRequestValidator()
    {
        RuleFor(r => r.Topic).MaximumLength(AttendanceSession.MaxTopicLength)
            .WithErrorCode("TOPIC_TOO_LONG").WithMessage($"Chủ đề tối đa {AttendanceSession.MaxTopicLength} ký tự.");
        RuleFor(r => r.NextHomework).MaximumLength(AttendanceSession.MaxHomeworkLength)
            .WithErrorCode("HOMEWORK_TOO_LONG").WithMessage($"BTVN tối đa {AttendanceSession.MaxHomeworkLength} ký tự.");
        RuleFor(r => r.Records!.Count).LessThanOrEqualTo(AttendanceService.MaxStudents).When(r => r.Records is not null)
            .OverridePropertyName("records").WithErrorCode("TOO_MANY_RECORDS")
            .WithMessage($"Mỗi lần lưu tối đa {AttendanceService.MaxStudents} học sinh.");
        RuleForEach(r => r.Records).ChildRules(record =>
        {
            record.RuleFor(x => x.Status).IsInEnum().WithErrorCode("STATUS_INVALID").WithMessage("Trạng thái điểm danh không hợp lệ.");
            record.RuleFor(x => x.HomeworkStatus).IsInEnum().WithErrorCode("HOMEWORK_STATUS_INVALID").WithMessage("Mức BTVN không hợp lệ.");
            record.RuleFor(x => x.HomeworkScore).Must(s => s is null || AttendanceRecord.IsValidScore(s.Value))
                .WithErrorCode("SCORE_INVALID").WithMessage("Điểm BTVN từ 0 đến 10, bước 0,5.");
            record.RuleFor(x => x.Note).MaximumLength(AttendanceRecord.MaxNoteLength)
                .WithErrorCode("NOTE_TOO_LONG").WithMessage($"Nhận xét tối đa {AttendanceRecord.MaxNoteLength} ký tự.");
        });
    }
}

internal sealed class UpdateStudentContactRequestValidator : AbstractValidator<UpdateStudentContactRequest>
{
    public UpdateStudentContactRequestValidator()
    {
        RuleFor(r => r.ParentName).MaximumLength(StudentContact.MaxParentNameLength)
            .WithErrorCode("PARENT_NAME_TOO_LONG").WithMessage($"Tên phụ huynh tối đa {StudentContact.MaxParentNameLength} ký tự.");
        RuleFor(r => r.ParentPhone).Must(p => string.IsNullOrWhiteSpace(p) || StudentContact.IsValidPhone(p.Trim()))
            .WithErrorCode("PHONE_INVALID").WithMessage("Số điện thoại không hợp lệ (8–20 ký tự: chữ số, +, khoảng trắng, dấu chấm, gạch ngang).");
    }
}

/// <summary>
/// Điểm danh + BTVN theo buổi (D-31, docs/02-nghiep-vu.md mục 5.2). Giáo viên chỉ thấy lớp mình chủ nhiệm;
/// Attendance.ManageAll thấy mọi lớp. Lớp ngoài phạm vi → 404 (quy tắc 10).
/// </summary>
internal sealed class AttendanceService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    IUserAccessService userAccess,
    IAttendanceExporter exporter,
    IOptions<AppOptions> appOptions,
    TimeProvider time,
    IValidator<SaveAttendanceRequest> saveValidator,
    IValidator<UpdateStudentContactRequest> contactValidator) : IAttendanceService
{
    /// <summary>Giới hạn sĩ số đọc / ghi trong một request (cùng giới hạn thêm học viên vào lớp).</summary>
    public const int MaxStudents = ELearning.Application.Classes.ClassroomService.MaxStudentsPerRequest;

    public const int MaxClasses = 100;

    private static readonly Error ClassroomNotFound = Error.NotFound(ErrorCodes.ClassroomNotFound, "Không tìm thấy lớp học.");

    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<AttendanceClassDto>> ListClassesAsync(AttendanceClassQuery query, CancellationToken ct)
    {
        var classes = await AccessibleClasses(ct);
        classes = classes.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            classes = classes.Where(c => EF.Functions.ILike(c.Code, kw) || EF.Functions.ILike(c.Name, kw));
        }

        return await classes.OrderBy(c => c.Code).Take(MaxClasses)
            .Select(c => new AttendanceClassDto(
                c.Id, c.Code, c.Name, c.SchoolYear, c.IsActive, c.Students.Count,
                db.Users.Where(u => u.Id == c.HomeroomTeacherId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<Result<AttendanceSessionDto>> GetSessionAsync(Guid classroomId, DateOnly date, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (ValidateDate(date) is { } dateError)
        {
            return dateError;
        }

        return await LoadSessionAsync(classroom, date, ct);
    }

    public async Task<Result<SaveAttendanceResultDto>> SaveSessionAsync(
        Guid classroomId, DateOnly date, SaveAttendanceRequest request, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (ValidateDate(date) is { } dateError)
        {
            return dateError;
        }

        var errors = await saveValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<SaveAttendanceResultDto>.Failure(errors);
        }

        var records = (request.Records ?? []).GroupBy(r => r.StudentId).Select(g => g.Last()).ToList();
        var session = await db.AttendanceSessions.Include(s => s.Records)
            .SingleOrDefaultAsync(s => s.ClassroomId == classroomId && s.SessionDate == date, ct);

        // Chỉ ghi cho học sinh đang trong lớp, hoặc đã có dữ liệu ở buổi này (học sinh đã chuyển lớp sau buổi học)
        var ids = records.Select(r => r.StudentId).ToList();
        var allowed = await db.ClassroomStudents.Where(s => s.ClassroomId == classroomId && ids.Contains(s.UserId))
            .Select(s => s.UserId).ToListAsync(ct);
        allowed.AddRange(session?.Records.Select(r => r.StudentId) ?? []);
        if (ids.Except(allowed).Any())
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có học sinh không thuộc lớp.", "records");
        }

        var userId = currentUser.RequiredUserId;
        if (session is null)
        {
            session = new AttendanceSession(classroomId, date, userId, Now);
            db.AttendanceSessions.Add(session);
        }

        session.UpdateNotes(request.Topic, request.NextHomework, userId, Now);
        foreach (var r in records)
        {
            session.Mark(r.StudentId, new AttendanceEntry(r.Status, r.HomeworkStatus, r.HomeworkScore, r.Note), userId, Now);
        }

        // Hai người lưu buổi mới cùng lúc → UQ_AttendanceSessions_Classroom_Date báo trùng → 409, client lưu lại
        await db.SaveChangesAsync(ct);
        return new SaveAttendanceResultDto(session.UpdatedAt ?? Now, records.Count);
    }

    public async Task<Result<AttendanceSessionDto>> ResetSessionAsync(Guid classroomId, DateOnly date, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        var session = await db.AttendanceSessions.Include(s => s.Records)
            .SingleOrDefaultAsync(s => s.ClassroomId == classroomId && s.SessionDate == date, ct);
        if (session is not null)
        {
            var old = new
            {
                session.Topic,
                session.NextHomework,
                Records = session.Records.Where(r => r.Status is not null || r.HomeworkStatus is not null || r.HomeworkScore is not null || r.Note is not null)
                    .Select(r => new { r.StudentId, r.Status, r.HomeworkStatus, r.HomeworkScore, r.Note }).ToList(),
            };
            session.Reset(currentUser.RequiredUserId, Now);
            audit.Write(AuditActions.AttendanceSessionReset, nameof(AttendanceSession), session.Id, old, new { classroom.Code, SessionDate = date });
            await db.SaveChangesAsync(ct);
        }

        return await LoadSessionAsync(classroom, date, ct);
    }

    public async Task<Result<StudentContactDto>> UpdateContactAsync(
        Guid classroomId, Guid studentId, UpdateStudentContactRequest request, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (!await db.ClassroomStudents.AnyAsync(s => s.ClassroomId == classroomId && s.UserId == studentId, ct))
        {
            return Error.NotFound(ErrorCodes.UserNotFound, "Học sinh không thuộc lớp.");
        }

        var errors = await contactValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<StudentContactDto>.Failure(errors);
        }

        var contact = await db.StudentContacts.SingleOrDefaultAsync(c => c.UserId == studentId, ct);
        if (contact is null)
        {
            contact = new StudentContact(studentId);
            db.StudentContacts.Add(contact);
        }

        var old = new { contact.ParentName, contact.ParentPhone };
        contact.Update(request.ParentName, request.ParentPhone, currentUser.RequiredUserId, Now);
        audit.Write(AuditActions.StudentContactUpdated, "User", studentId, old, new { contact.ParentName, contact.ParentPhone });
        await db.SaveChangesAsync(ct);
        return new StudentContactDto(studentId, contact.ParentName, contact.ParentPhone);
    }

    public async Task<Result<AttendanceStudentHistoryDto>> GetStudentHistoryAsync(
        Guid classroomId, Guid studentId, AttendanceHistoryQuery query, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        return classroom is null ? ClassroomNotFound : await BuildHistoryAsync(classroom, studentId, query, ct);
    }

    public async Task<Result<AttendanceStudentHistoryDto>> GetMyHistoryAsync(Guid classroomId, AttendanceHistoryQuery query, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        // Học sinh chỉ xem được của chính mình; không gửi thông tin liên hệ phụ huynh (chỉ giáo viên dùng)
        var result = await BuildHistoryAsync(classroom, currentUser.RequiredUserId, query, ct);
        if (result.IsFailure)
        {
            return result.FirstError.Code == ErrorCodes.UserNotFound ? ClassroomNotFound : result.FirstError;
        }

        return result.Value with { ParentName = null, ParentPhone = null };
    }

    private async Task<Result<AttendanceStudentHistoryDto>> BuildHistoryAsync(
        Classroom classroom, Guid studentId, AttendanceHistoryQuery query, CancellationToken ct)
    {
        var classroomId = classroom.Id;
        if (!TryParseMonth(query.Month, out var year, out var month))
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Tháng phải có dạng yyyy-MM.", "month");
        }

        var student = await (
                from u in db.Users.AsNoTracking()
                where u.Id == studentId
                join c in db.StudentContacts on u.Id equals c.UserId into cs
                from c in cs.DefaultIfEmpty()
                select new { u.UserName, u.FullName, ParentName = c != null ? c.ParentName : null, ParentPhone = c != null ? c.ParentPhone : null })
            .SingleOrDefaultAsync(ct);
        var belongs = await db.ClassroomStudents.AnyAsync(s => s.ClassroomId == classroomId && s.UserId == studentId, ct)
            || await db.AttendanceRecords.AnyAsync(r => r.StudentId == studentId
                && db.AttendanceSessions.Any(s => s.Id == r.SessionId && s.ClassroomId == classroomId), ct);
        if (student is null || !belongs)
        {
            return Error.NotFound(ErrorCodes.UserNotFound, "Học sinh không thuộc lớp.");
        }

        var sheet = await LoadMonthAsync(classroomId, year, month, [studentId], ct);
        var cells = sheet.Cells.GetValueOrDefault(studentId) ?? [];
        var items = sheet.Sessions.Select((s, i) => cells.ElementAtOrDefault(i) ?? new AttendanceHistoryItemDto(s.Date, s.Topic, null, null, null, null))
            .ToList();

        // Thứ hạng theo điểm BTVN trung bình tháng trong lớp (học sinh không có điểm nào thì không xếp hạng)
        var averages = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.HomeworkScore != null && db.AttendanceSessions.Any(s => s.Id == r.SessionId && s.ClassroomId == classroomId
                && s.SessionDate >= sheet.From && s.SessionDate <= sheet.To))
            .GroupBy(r => r.StudentId)
            .Select(g => new { StudentId = g.Key, Average = g.Average(r => r.HomeworkScore!.Value) })
            .ToListAsync(ct);
        var mine = averages.Find(a => a.StudentId == studentId)?.Average;
        int? rank = mine is { } avg ? 1 + averages.Count(a => a.Average > avg) : null;

        return new AttendanceStudentHistoryDto(
            studentId, student.UserName, student.FullName, student.ParentName, student.ParentPhone, classroom.Name, year, month,
            mine is { } m ? Math.Round(m, 2) : null, rank, averages.Count, items);
    }

    public async Task<Result<ExportFile>> ExportAsync(Guid classroomId, DateOnly date, CancellationToken ct)
    {
        var classroom = await FindClassAsync(classroomId, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        var session = await LoadSessionAsync(classroom, date, ct);
        var ids = session.Students.Select(s => s.StudentId).ToList();
        var month = await LoadMonthAsync(classroomId, date.Year, date.Month, ids, ct);
        var rows = session.Students.Select(s => new AttendanceMonthRow(
                s.UserName, s.FullName, month.Cells.GetValueOrDefault(s.StudentId) ?? month.Sessions.Select(_ => (AttendanceHistoryItemDto?)null).ToList()))
            .ToList();
        var sheet = new AttendanceMonthSheet(date.Year, date.Month, month.Sessions.Select(s => s.Date).ToList(), rows);
        var content = exporter.Export(session, sheet);
        return new ExportFile(
            $"diem-danh-{classroom.Code}-{date:yyyy-MM-dd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            content);
    }

    private async Task<bool> CanManageAllAsync(CancellationToken ct) =>
        (await userAccess.GetAsync(currentUser.RequiredUserId, ct))?.Permissions.Contains(Permissions.AttendanceManageAll) == true;

    private async Task<IQueryable<Classroom>> AccessibleClasses(CancellationToken ct)
    {
        var classes = db.Classrooms.AsNoTracking();
        if (!await CanManageAllAsync(ct))
        {
            var userId = currentUser.RequiredUserId;
            classes = classes.Where(c => c.HomeroomTeacherId == userId);
        }

        return classes;
    }

    private async Task<Classroom?> FindClassAsync(Guid classroomId, CancellationToken ct) =>
        await (await AccessibleClasses(ct)).SingleOrDefaultAsync(c => c.Id == classroomId, ct);

    private DateOnly Today()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(appOptions.Value.BusinessTimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, zone));
    }

    /// <summary>Không điểm danh trước cho ngày tương lai (cho phép lệch 1 ngày vì múi giờ).</summary>
    private Error? ValidateDate(DateOnly date) =>
        date > Today().AddDays(1) || date.Year < 2000
            ? Error.Validation(ErrorCodes.ValidationFailed, "Không điểm danh cho ngày trong tương lai.", "date")
            : null;

    private bool TryParseMonth(string? value, out int year, out int month)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var today = Today();
            (year, month) = (today.Year, today.Month);
            return true;
        }

        if (DateOnly.TryParseExact(value.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            && parsed.Year >= 2000)
        {
            (year, month) = (parsed.Year, parsed.Month);
            return true;
        }

        (year, month) = (0, 0);
        return false;
    }

    private async Task<AttendanceSessionDto> LoadSessionAsync(Classroom classroom, DateOnly date, CancellationToken ct)
    {
        var session = await db.AttendanceSessions.AsNoTracking().Include(s => s.Records)
            .SingleOrDefaultAsync(s => s.ClassroomId == classroom.Id && s.SessionDate == date, ct);
        var recordStudentIds = session?.Records.Select(r => r.StudentId).ToList() ?? [];
        var classStudentIds = db.ClassroomStudents.Where(s => s.ClassroomId == classroom.Id).Select(s => s.UserId);

        var students = await (
                from u in db.Users.AsNoTracking()
                where classStudentIds.Contains(u.Id) || recordStudentIds.Contains(u.Id)
                join c in db.StudentContacts on u.Id equals c.UserId into cs
                from c in cs.DefaultIfEmpty()
                select new
                {
                    u.Id,
                    u.UserName,
                    u.FullName,
                    ParentName = c != null ? c.ParentName : null,
                    ParentPhone = c != null ? c.ParentPhone : null,
                    InClass = classStudentIds.Contains(u.Id),
                })
            .Take(MaxStudents)
            .ToListAsync(ct);

        var records = session?.Records.ToDictionary(r => r.StudentId) ?? new Dictionary<Guid, AttendanceRecord>();
        var items = students
            .OrderBy(s => GivenName(s.FullName), NameComparer).ThenBy(s => s.FullName, NameComparer).ThenBy(s => s.UserName, StringComparer.Ordinal)
            .Select(s =>
            {
                var r = records.GetValueOrDefault(s.Id);
                return new AttendanceStudentDto(
                    s.Id, s.UserName, s.FullName, s.ParentName, s.ParentPhone, r?.Status, r?.HomeworkStatus, r?.HomeworkScore, r?.Note, s.InClass);
            })
            .ToList();

        string? updatedByName = null;
        if (session?.UpdatedBy is { } updatedBy)
        {
            updatedByName = await db.Users.Where(u => u.Id == updatedBy).Select(u => u.FullName).SingleOrDefaultAsync(ct);
        }

        return new AttendanceSessionDto(
            classroom.Id, classroom.Code, classroom.Name, date, session is not null, session?.Topic, session?.NextHomework,
            session?.UpdatedAt, updatedByName, items);
    }

    private sealed record MonthSession(Guid Id, DateOnly Date, string? Topic);

    private sealed record MonthData(DateOnly From, DateOnly To, IReadOnlyList<MonthSession> Sessions, Dictionary<Guid, List<AttendanceHistoryItemDto?>> Cells);

    private async Task<MonthData> LoadMonthAsync(Guid classroomId, int year, int month, IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var sessions = await db.AttendanceSessions.AsNoTracking()
            .Where(s => s.ClassroomId == classroomId && s.SessionDate >= from && s.SessionDate <= to)
            .OrderBy(s => s.SessionDate)
            .Select(s => new MonthSession(s.Id, s.SessionDate, s.Topic))
            .ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(r => sessionIds.Contains(r.SessionId) && studentIds.Contains(r.StudentId))
            .Select(r => new { r.SessionId, r.StudentId, r.Status, r.HomeworkStatus, r.HomeworkScore, r.Note })
            .ToListAsync(ct);

        var lookup = records.ToDictionary(r => (r.SessionId, r.StudentId));
        var cells = studentIds.ToDictionary(
            id => id,
            id => sessions.Select(s => lookup.TryGetValue((s.Id, id), out var r)
                    ? new AttendanceHistoryItemDto(s.Date, s.Topic, r.Status, r.HomeworkStatus, r.HomeworkScore, r.Note)
                    : null)
                .ToList());
        return new MonthData(from, to, sessions, cells);
    }

    /// <summary>Tên gọi (từ cuối) để xếp danh sách lớp theo thói quen Việt Nam: An, Bình, Chi…</summary>
    private static string GivenName(string fullName)
    {
        var trimmed = fullName.Trim();
        var space = trimmed.LastIndexOf(' ');
        return space < 0 ? trimmed : trimmed[(space + 1)..];
    }
}

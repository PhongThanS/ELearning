using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Classes;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Classes;

public sealed record ClassroomListQuery : PageRequest
{
    public string? Keyword { get; init; }

    public string? SchoolYear { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record ClassroomDto(
    Guid Id,
    string Code,
    string Name,
    string? SchoolYear,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description,
    bool IsActive,
    int StudentCount,
    int ExamCount,
    DateTime CreatedAt,
    string RowVersion);

public sealed record ClassroomStudentDto(Guid UserId, string UserName, string FullName, string Email, bool IsActive, DateTime JoinedAt);

/// <summary>Lớp của học viên (trang "Lớp của tôi").</summary>
public sealed record MyClassroomDto(
    Guid Id, string Code, string Name, string? SchoolYear, DateOnly? StartDate, DateOnly? EndDate, string? Description,
    int StudentCount, int ExamCount, DateTime JoinedAt);

public sealed record CreateClassroomRequest(
    string Code, string Name, string? SchoolYear, DateOnly? StartDate, DateOnly? EndDate, string? Description);

public sealed record UpdateClassroomRequest(
    string Name, string? SchoolYear, DateOnly? StartDate, DateOnly? EndDate, string? Description, bool IsActive, string RowVersion);

public sealed record SetClassroomStatusRequest(bool IsActive);

public sealed record AddClassroomStudentsRequest(IReadOnlyList<Guid> UserIds);

public sealed record ClassroomStudentQuery : PageRequest
{
    public string? Keyword { get; init; }
}

public interface IClassroomService
{
    Task<PagedResult<ClassroomDto>> ListAsync(ClassroomListQuery query, CancellationToken ct);

    Task<Result<ClassroomDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<ClassroomDto>> CreateAsync(CreateClassroomRequest request, CancellationToken ct);

    Task<Result<ClassroomDto>> UpdateAsync(Guid id, UpdateClassroomRequest request, CancellationToken ct);

    Task<Result<ClassroomDto>> SetStatusAsync(Guid id, SetClassroomStatusRequest request, CancellationToken ct);

    /// <summary>Xóa lớp và danh sách học viên của lớp. Lớp đang được gán cho đề thi thì không xóa được.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);

    Task<Result<PagedResult<ClassroomStudentDto>>> ListStudentsAsync(Guid id, ClassroomStudentQuery query, CancellationToken ct);

    Task<Result<ClassroomDto>> AddStudentsAsync(Guid id, AddClassroomStudentsRequest request, CancellationToken ct);

    Task<Result<ClassroomDto>> RemoveStudentAsync(Guid id, Guid userId, CancellationToken ct);

    /// <summary>Các lớp đang hoạt động mà học viên thuộc về.</summary>
    Task<IReadOnlyList<MyClassroomDto>> ListMineAsync(Guid userId, CancellationToken ct);
}

internal static class ClassroomRules
{
    public static void AddDetailsRules<T>(AbstractValidator<T> v, Func<T, string> name, Func<T, string?> schoolYear,
        Func<T, DateOnly?> start, Func<T, DateOnly?> end, Func<T, string?> description)
    {
        v.RuleFor(r => name(r)).NotEmpty().OverridePropertyName("name").WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên lớp.")
            .MaximumLength(Classroom.MaxNameLength).WithErrorCode("NAME_TOO_LONG").WithMessage($"Tên lớp tối đa {Classroom.MaxNameLength} ký tự.");
        v.RuleFor(r => schoolYear(r)).MaximumLength(Classroom.MaxSchoolYearLength).OverridePropertyName("schoolYear")
            .WithErrorCode("SCHOOL_YEAR_TOO_LONG").WithMessage($"Năm học tối đa {Classroom.MaxSchoolYearLength} ký tự.");
        v.RuleFor(r => description(r)).MaximumLength(Classroom.MaxDescriptionLength).OverridePropertyName("description");
        v.RuleFor(r => end(r)).Must((r, e) => e is null || start(r) is null || e >= start(r)).OverridePropertyName("endDate")
            .WithErrorCode("DATE_RANGE_INVALID").WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
    }
}

internal sealed class CreateClassroomRequestValidator : AbstractValidator<CreateClassroomRequest>
{
    public CreateClassroomRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().WithErrorCode("CODE_REQUIRED").WithMessage("Vui lòng nhập mã lớp.")
            .Matches("^[A-Za-z0-9._-]{2,50}$").WithErrorCode("CODE_INVALID")
            .WithMessage("Mã lớp gồm 2–50 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang.");
        ClassroomRules.AddDetailsRules(this, r => r.Name, r => r.SchoolYear, r => r.StartDate, r => r.EndDate, r => r.Description);
    }
}

internal sealed class UpdateClassroomRequestValidator : AbstractValidator<UpdateClassroomRequest>
{
    public UpdateClassroomRequestValidator()
    {
        ClassroomRules.AddDetailsRules(this, r => r.Name, r => r.SchoolYear, r => r.StartDate, r => r.EndDate, r => r.Description);
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

/// <summary>Lớp học và học viên của lớp (D-28, docs/02-nghiep-vu.md mục 5.1, docs/05-api.md).</summary>
internal sealed class ClassroomService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateClassroomRequest> createValidator,
    IValidator<UpdateClassroomRequest> updateValidator) : IClassroomService
{
    public const int MaxStudentsPerRequest = 500;

    private static readonly Error ClassroomNotFound = Error.NotFound(ErrorCodes.ClassroomNotFound, "Không tìm thấy lớp học.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<ClassroomDto>> ListAsync(ClassroomListQuery query, CancellationToken ct)
    {
        var classes = db.Classrooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            classes = classes.Where(c => EF.Functions.Like(c.Code, kw) || EF.Functions.Like(c.Name, kw));
        }

        if (!string.IsNullOrWhiteSpace(query.SchoolYear))
        {
            var year = query.SchoolYear.Trim();
            classes = classes.Where(c => c.SchoolYear == year);
        }

        if (query.IsActive is { } isActive)
        {
            classes = classes.Where(c => c.IsActive == isActive);
        }

        classes = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => classes.OrderBy(c => c.Name),
            ("name", true) => classes.OrderByDescending(c => c.Name),
            ("schoolyear", false) => classes.OrderBy(c => c.SchoolYear).ThenBy(c => c.Code),
            ("schoolyear", true) => classes.OrderByDescending(c => c.SchoolYear).ThenBy(c => c.Code),
            ("createdat", false) => classes.OrderBy(c => c.CreatedAt),
            ("createdat", true) => classes.OrderByDescending(c => c.CreatedAt),
            (_, true) => classes.OrderByDescending(c => c.Code),
            _ => classes.OrderBy(c => c.Code),
        };

        var total = await classes.CountAsync(ct);
        var items = await classes.Skip(query.Skip).Take(query.PageSize).Select(ToDto()).ToListAsync(ct);
        return new PagedResult<ClassroomDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<ClassroomDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.Classrooms.AsNoTracking().Where(c => c.Id == id).Select(ToDto()).SingleOrDefaultAsync(ct);
        return dto is null ? ClassroomNotFound : dto;
    }

    public async Task<Result<ClassroomDto>> CreateAsync(CreateClassroomRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<ClassroomDto>.Failure(errors);
        }

        var code = request.Code.Trim();
        if (await db.Classrooms.AnyAsync(c => c.Code == code, ct))
        {
            return Error.Conflict(ErrorCodes.DuplicateCode, "Mã lớp đã tồn tại.");
        }

        var details = new ClassroomDetails(request.Name, request.SchoolYear, request.StartDate, request.EndDate, request.Description);
        var classroom = new Classroom(code, details, currentUser.RequiredUserId, Now);
        db.Classrooms.Add(classroom);
        audit.Write(AuditActions.ClassroomCreated, nameof(Classroom), classroom.Id, newValue: new { classroom.Code, classroom.Name, classroom.SchoolYear });
        await db.SaveChangesAsync(ct);
        return await GetAsync(classroom.Id, ct);
    }

    public async Task<Result<ClassroomDto>> UpdateAsync(Guid id, UpdateClassroomRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<ClassroomDto>.Failure(errors);
        }

        var classroom = await db.Classrooms.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (!db.TryApplyRowVersion(classroom, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        var old = new { classroom.Name, classroom.SchoolYear, classroom.StartDate, classroom.EndDate, classroom.IsActive };
        var details = new ClassroomDetails(request.Name, request.SchoolYear, request.StartDate, request.EndDate, request.Description);
        classroom.Update(details, currentUser.RequiredUserId, Now);
        if (classroom.IsActive != request.IsActive)
        {
            classroom.SetActive(request.IsActive, currentUser.RequiredUserId, Now);
        }

        audit.Write(AuditActions.ClassroomUpdated, nameof(Classroom), classroom.Id, old,
            new { classroom.Name, classroom.SchoolYear, classroom.StartDate, classroom.EndDate, classroom.IsActive });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<ClassroomDto>> SetStatusAsync(Guid id, SetClassroomStatusRequest request, CancellationToken ct)
    {
        var classroom = await db.Classrooms.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (classroom.IsActive != request.IsActive)
        {
            classroom.SetActive(request.IsActive, currentUser.RequiredUserId, Now);
            audit.Write(AuditActions.ClassroomUpdated, nameof(Classroom), classroom.Id, new { IsActive = !request.IsActive }, new { request.IsActive });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var classroom = await db.Classrooms.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        // Xóa ngầm phần gán sẽ đổi người được thi của đề → bắt bỏ gán trước (hoặc tắt lớp)
        var examCount = await db.ExamAssignments.CountAsync(a => a.ClassroomId == id, ct);
        if (examCount > 0)
        {
            return Error.Conflict(
                ErrorCodes.ClassroomInUse,
                $"Lớp đang được gán cho {examCount} đề thi. Bỏ gán lớp khỏi các đề đó trước, hoặc tắt lớp thay vì xóa.");
        }

        var students = await db.ClassroomStudents.Where(s => s.ClassroomId == id).ToListAsync(ct);
        db.ClassroomStudents.RemoveRange(students);
        db.Classrooms.Remove(classroom);
        audit.Write(AuditActions.ClassroomDeleted, nameof(Classroom), classroom.Id,
            new { classroom.Code, classroom.Name, classroom.SchoolYear, StudentIds = students.Select(s => s.UserId).ToList() });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PagedResult<ClassroomStudentDto>>> ListStudentsAsync(Guid id, ClassroomStudentQuery query, CancellationToken ct)
    {
        if (!await db.Classrooms.AnyAsync(c => c.Id == id, ct))
        {
            return ClassroomNotFound;
        }

        var students = db.ClassroomStudents.AsNoTracking().Where(s => s.ClassroomId == id)
            .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s, u });
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            students = students.Where(x => EF.Functions.Like(x.u.UserName, kw) || EF.Functions.Like(x.u.FullName, kw) || EF.Functions.Like(x.u.Email, kw));
        }

        var total = await students.CountAsync(ct);
        var items = await students.OrderBy(x => x.u.FullName).ThenBy(x => x.u.UserName).Skip(query.Skip).Take(query.PageSize)
            .Select(x => new ClassroomStudentDto(x.u.Id, x.u.UserName, x.u.FullName, x.u.Email, x.u.IsActive, x.s.JoinedAt))
            .ToListAsync(ct);
        return new PagedResult<ClassroomStudentDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<ClassroomDto>> AddStudentsAsync(Guid id, AddClassroomStudentsRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds.Distinct().ToList();
        if (userIds.Count is 0 or > MaxStudentsPerRequest)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, $"Danh sách học viên phải có từ 1 đến {MaxStudentsPerRequest} người.", "userIds");
        }

        var classroom = await db.Classrooms.Include(c => c.Students).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        // Chỉ nhận tài khoản có vai trò học viên (lớp là nơi học viên nhận đề)
        var students = await db.Users.CountAsync(
            u => userIds.Contains(u.Id) && u.Roles.Any(r => r.Role.Code == Role.StudentCode), ct);
        if (students != userIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có người dùng không tồn tại hoặc không phải học viên.", "userIds");
        }

        var added = classroom.AddStudents(userIds, Now);
        if (added > 0)
        {
            audit.Write(AuditActions.ClassroomStudentsChanged, nameof(Classroom), classroom.Id, newValue: new { Added = userIds });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<Result<ClassroomDto>> RemoveStudentAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var classroom = await db.Classrooms.Include(c => c.Students).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (classroom is null)
        {
            return ClassroomNotFound;
        }

        if (classroom.RemoveStudent(userId))
        {
            audit.Write(AuditActions.ClassroomStudentsChanged, nameof(Classroom), classroom.Id, newValue: new { Removed = new[] { userId } });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<MyClassroomDto>> ListMineAsync(Guid userId, CancellationToken ct) =>
        await db.ClassroomStudents.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Join(db.Classrooms.Where(c => c.IsActive), s => s.ClassroomId, c => c.Id, (s, c) => new { s, c })
            .OrderBy(x => x.c.Name)
            .Take(PageRequest.MaxPageSize)
            .Select(x => new MyClassroomDto(
                x.c.Id,
                x.c.Code,
                x.c.Name,
                x.c.SchoolYear,
                x.c.StartDate,
                x.c.EndDate,
                x.c.Description,
                x.c.Students.Count,
                db.Exams.Count(e => e.Status != ExamStatus.Draft && e.Assignments.Any(a => a.ClassroomId == x.c.Id)),
                x.s.JoinedAt))
            .ToListAsync(ct);

    private System.Linq.Expressions.Expression<Func<Classroom, ClassroomDto>> ToDto() =>
        c => new ClassroomDto(
            c.Id,
            c.Code,
            c.Name,
            c.SchoolYear,
            c.StartDate,
            c.EndDate,
            c.Description,
            c.IsActive,
            c.Students.Count,
            db.Exams.Count(e => e.Assignments.Any(a => a.ClassroomId == c.Id)),
            c.CreatedAt,
            Convert.ToBase64String(c.RowVersion));
}

using ELearning.Domain.Common;

namespace ELearning.Domain.Classes;

/// <summary>
/// Lớp học (D-28, docs/02-nghiep-vu.md mục 5.1). Học viên và lớp là quan hệ nhiều-nhiều: một học viên học nhiều lớp,
/// một lớp có nhiều học viên (ClassroomStudents). Lớp được gán đề thi giống nhóm. Không xóa cứng (D-16): ngừng dùng thì tắt.
/// </summary>
public sealed class Classroom : Entity, IHasRowVersion
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;
    public const int MaxSchoolYearLength = 20;
    public const int MaxDescriptionLength = 1000;

    private readonly List<ClassroomStudent> _students = [];

    private Classroom()
    {
    }

    public Classroom(string code, ClassroomDetails details, Guid createdBy, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Apply(details);
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Năm học / học kỳ, ví dụ "2026-2027" hoặc "HK1 2026-2027".</summary>
    public string? SchoolYear { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ClassroomStudent> Students => _students;

    public void Update(ClassroomDetails details, Guid updatedBy, DateTime now)
    {
        Apply(details);
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SetActive(bool isActive, Guid updatedBy, DateTime now)
    {
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>Thêm học viên chưa có trong lớp; trả về số học viên được thêm.</summary>
    public int AddStudents(IEnumerable<Guid> userIds, DateTime now)
    {
        var added = 0;
        foreach (var userId in userIds.Distinct().Where(id => _students.TrueForAll(s => s.UserId != id)))
        {
            _students.Add(new ClassroomStudent(Id, userId, now));
            added++;
        }

        return added;
    }

    public bool RemoveStudent(Guid userId) => _students.RemoveAll(s => s.UserId == userId) > 0;

    private void Apply(ClassroomDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Name);
        if (details.StartDate is { } start && details.EndDate is { } end && end < start)
        {
            throw new DomainException(DomainErrorCodes.InvalidClassroom, "Ngày kết thúc phải sau ngày bắt đầu.");
        }

        Name = details.Name.Trim();
        SchoolYear = string.IsNullOrWhiteSpace(details.SchoolYear) ? null : details.SchoolYear.Trim();
        StartDate = details.StartDate;
        EndDate = details.EndDate;
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
    }
}

public sealed record ClassroomDetails(string Name, string? SchoolYear, DateOnly? StartDate, DateOnly? EndDate, string? Description);

/// <summary>Học viên thuộc lớp (bảng nối nhiều-nhiều Users ↔ Classrooms).</summary>
public sealed class ClassroomStudent
{
    private ClassroomStudent()
    {
    }

    public ClassroomStudent(Guid classroomId, Guid userId, DateTime joinedAt)
    {
        ClassroomId = classroomId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid ClassroomId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime JoinedAt { get; private set; }
}

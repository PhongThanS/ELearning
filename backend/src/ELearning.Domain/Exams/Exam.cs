using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Exams;

/// <summary>Thông tin đề sửa được (kể cả sau publish, có audit — D-03).</summary>
public sealed record ExamDetails(
    string Name,
    string? Description,
    string? Instructions,
    DateTime? StartAt,
    DateTime? EndAt,
    int MaxAttempts,
    AccessMode AccessMode,
    RetakeScoringPolicy RetakeScoringPolicy);

/// <summary>
/// Đề thi: metadata, lịch thi, số lượt, quyền dự thi (docs/02-nghiep-vu.md mục 4).
/// Trạng thái: DRAFT → PUBLISHED ⇄ CLOSED (D-04).
/// </summary>
public sealed class Exam : Entity, IHasRowVersion
{
    public const int MinAttempts = 1;
    public const int MaxAttemptsLimit = 50;

    private readonly List<ExamVersion> _versions = [];
    private readonly List<ExamAssignment> _assignments = [];

    private Exam()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? Instructions { get; private set; }

    public ExamStatus Status { get; private set; }

    public DateTime? StartAt { get; private set; }

    public DateTime? EndAt { get; private set; }

    public int MaxAttempts { get; private set; }

    public AccessMode AccessMode { get; private set; }

    public RetakeScoringPolicy RetakeScoringPolicy { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ExamVersion> Versions => _versions;

    public IReadOnlyCollection<ExamAssignment> Assignments => _assignments;

    /// <summary>Tạo đề DRAFT kèm version 1 DRAFT.</summary>
    public static Exam Create(string code, ExamDetails details, VersionSettings settings, Guid createdBy, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var exam = new Exam
        {
            Code = code.Trim(),
            Status = ExamStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = now,
        };
        exam.ApplyDetails(details);
        exam._versions.Add(ExamVersion.CreateDraft(exam.Id, 1, settings, createdBy, now));
        return exam;
    }

    public void UpdateDetails(ExamDetails details, Guid updatedBy, DateTime now)
    {
        ApplyDetails(details);
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>Chỉ đổi được mã khi đề chưa từng publish.</summary>
    public void ChangeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureStatus(ExamStatus.Draft, "Chỉ đổi được mã khi đề chưa từng publish.");
        Code = code.Trim();
    }

    public void MarkPublished(Guid updatedBy, DateTime now)
    {
        if (Status == ExamStatus.Draft)
        {
            Status = ExamStatus.Published;
        }

        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Close(Guid updatedBy, DateTime now)
    {
        EnsureStatus(ExamStatus.Published, "Chỉ đóng được đề đang mở.");
        Status = ExamStatus.Closed;
        ClosedAt = now;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Reopen(Guid updatedBy, DateTime now)
    {
        EnsureStatus(ExamStatus.Closed, "Chỉ mở lại được đề đã đóng.");
        Status = ExamStatus.Published;
        ClosedAt = null;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>Thay toàn bộ danh sách gán; trả về true nếu có thay đổi.</summary>
    public bool SetAssignments(
        IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<Guid> userIds, Guid by, DateTime now, IReadOnlyCollection<Guid>? classroomIds = null)
    {
        var targetGroups = groupIds.ToHashSet();
        var targetUsers = userIds.ToHashSet();
        var targetClasses = (classroomIds ?? []).ToHashSet();
        var removed = _assignments.RemoveAll(a =>
            (a.GroupId is { } g && !targetGroups.Contains(g))
            || (a.UserId is { } u && !targetUsers.Contains(u))
            || (a.ClassroomId is { } c && !targetClasses.Contains(c)));

        var added = 0;
        foreach (var classroomId in targetClasses.Where(c => _assignments.TrueForAll(a => a.ClassroomId != c)))
        {
            _assignments.Add(ExamAssignment.ForClassroom(Id, classroomId, by, now));
            added++;
        }

        foreach (var groupId in targetGroups.Where(g => _assignments.TrueForAll(a => a.GroupId != g)))
        {
            _assignments.Add(ExamAssignment.ForGroup(Id, groupId, by, now));
            added++;
        }

        foreach (var userId in targetUsers.Where(u => _assignments.TrueForAll(a => a.UserId != u)))
        {
            _assignments.Add(ExamAssignment.ForUser(Id, userId, by, now));
            added++;
        }

        return removed + added > 0;
    }

    private void ApplyDetails(ExamDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Name);
        if (details.MaxAttempts is < MinAttempts or > MaxAttemptsLimit)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, $"Số lượt thi phải từ {MinAttempts} đến {MaxAttemptsLimit}.");
        }

        if (details.StartAt is { } start && details.EndAt is { } end && start >= end)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, "Thời điểm bắt đầu phải trước thời điểm kết thúc.");
        }

        Name = details.Name.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Instructions = string.IsNullOrWhiteSpace(details.Instructions) ? null : details.Instructions.Trim();
        StartAt = details.StartAt;
        EndAt = details.EndAt;
        MaxAttempts = details.MaxAttempts;
        AccessMode = details.AccessMode;
        RetakeScoringPolicy = details.RetakeScoringPolicy;
    }

    private void EnsureStatus(ExamStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, message);
        }
    }
}

/// <summary>Gán đề cho một nhóm hoặc một user (đúng một trong hai).</summary>
public sealed class ExamAssignment : Entity
{
    private ExamAssignment()
    {
    }

    public Guid ExamId { get; private set; }

    public Guid? GroupId { get; private set; }

    public Guid? UserId { get; private set; }

    /// <summary>Gán cho lớp học (D-28): mọi học viên đang thuộc lớp (lớp đang hoạt động) thấy đề.</summary>
    public Guid? ClassroomId { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    internal static ExamAssignment ForClassroom(Guid examId, Guid classroomId, Guid by, DateTime now) =>
        new() { ExamId = examId, ClassroomId = classroomId, CreatedBy = by, CreatedAt = now };

    internal static ExamAssignment ForGroup(Guid examId, Guid groupId, Guid by, DateTime now) =>
        new() { ExamId = examId, GroupId = groupId, CreatedBy = by, CreatedAt = now };

    internal static ExamAssignment ForUser(Guid examId, Guid userId, Guid by, DateTime now) =>
        new() { ExamId = examId, UserId = userId, CreatedBy = by, CreatedAt = now };
}

/// <summary>Cấp thêm lượt thi cho một học viên.</summary>
public sealed class ExamUserOverride
{
    private ExamUserOverride()
    {
    }

    public ExamUserOverride(Guid examId, Guid userId, int extraAttempts, string? note, Guid updatedBy, DateTime updatedAt)
    {
        ExamId = examId;
        UserId = userId;
        Set(extraAttempts, note, updatedBy, updatedAt);
    }

    public Guid ExamId { get; private set; }

    public Guid UserId { get; private set; }

    public int ExtraAttempts { get; private set; }

    public string? Note { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public void Set(int extraAttempts, string? note, Guid updatedBy, DateTime updatedAt)
    {
        if (extraAttempts is < 0 or > Exam.MaxAttemptsLimit)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, $"Số lượt cấp thêm phải từ 0 đến {Exam.MaxAttemptsLimit}.");
        }

        ExtraAttempts = extraAttempts;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }
}

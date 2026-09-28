using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Exams;

/// <summary>
/// Đề thi: metadata, lịch thi, số lượt, quyền dự thi (sửa được sau publish, có audit — D-03).
/// Hành vi được bổ sung ở milestone M4.
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

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }
}

/// <summary>Cấp thêm lượt thi cho một học viên.</summary>
public sealed class ExamUserOverride
{
    private ExamUserOverride()
    {
    }

    public Guid ExamId { get; private set; }

    public Guid UserId { get; private set; }

    public int ExtraAttempts { get; private set; }

    public string? Note { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }
}

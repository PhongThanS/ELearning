using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Attendance;

/// <summary>
/// Một buổi học của lớp trong một ngày (D-31, docs/02-nghiep-vu.md mục 5.2): chủ đề, BTVN giao về và điểm danh /
/// điểm BTVN của từng học sinh. Mỗi lớp có tối đa một buổi mỗi ngày; buổi được tạo khi lưu lần đầu.
/// </summary>
public sealed class AttendanceSession : Entity
{
    public const int MaxTopicLength = 500;
    public const int MaxHomeworkLength = 1000;

    /// <summary>Số mức phân loại học sinh trong buổi (1..5, D-47).</summary>
    public const int LevelCount = 5;
    public const int MaxLevelCommentLength = 500;

    private readonly List<AttendanceRecord> _records = [];

    private AttendanceSession()
    {
    }

    public AttendanceSession(Guid classroomId, DateOnly sessionDate, Guid createdBy, DateTime createdAt)
    {
        ClassroomId = classroomId;
        SessionDate = sessionDate;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid ClassroomId { get; private set; }

    public DateOnly SessionDate { get; private set; }

    /// <summary>Chủ đề / nội dung bài học của buổi.</summary>
    public string? Topic { get; private set; }

    /// <summary>BTVN giao cho buổi sau.</summary>
    public string? NextHomework { get; private set; }

    /// <summary>Nhận xét chung cho học sinh phân loại 1..5 (D-47): mọi em cùng loại nhận cùng nhận xét.</summary>
    public string? LevelComment1 { get; private set; }

    public string? LevelComment2 { get; private set; }

    public string? LevelComment3 { get; private set; }

    public string? LevelComment4 { get; private set; }

    public string? LevelComment5 { get; private set; }

    /// <summary>Nhận xét chung theo phân loại, phần tử 0 là loại 1.</summary>
    public IReadOnlyList<string?> LevelComments => [LevelComment1, LevelComment2, LevelComment3, LevelComment4, LevelComment5];

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<AttendanceRecord> Records => _records;

    public void UpdateNotes(string? topic, string? nextHomework, Guid updatedBy, DateTime now)
    {
        Topic = Clean(topic, MaxTopicLength);
        NextHomework = Clean(nextHomework, MaxHomeworkLength);
        Touch(updatedBy, now);
    }

    /// <summary>Ghi nhận xét chung của 5 mức phân loại (đủ 5 phần tử, phần tử 0 là loại 1).</summary>
    public void UpdateLevelComments(IReadOnlyList<string?> comments, Guid updatedBy, DateTime now)
    {
        if (comments.Count != LevelCount)
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, $"Cần đủ {LevelCount} nhận xét phân loại.");
        }

        LevelComment1 = Clean(comments[0], MaxLevelCommentLength);
        LevelComment2 = Clean(comments[1], MaxLevelCommentLength);
        LevelComment3 = Clean(comments[2], MaxLevelCommentLength);
        LevelComment4 = Clean(comments[3], MaxLevelCommentLength);
        LevelComment5 = Clean(comments[4], MaxLevelCommentLength);
        Touch(updatedBy, now);
    }

    /// <summary>Ghi (thêm hoặc sửa) điểm danh / BTVN của một học sinh.</summary>
    public AttendanceRecord Mark(Guid studentId, AttendanceEntry entry, Guid updatedBy, DateTime now)
    {
        var record = _records.Find(r => r.StudentId == studentId);
        if (record is null)
        {
            record = new AttendanceRecord(Id, studentId);
            _records.Add(record);
        }

        record.Apply(entry, updatedBy, now);
        Touch(updatedBy, now);
        return record;
    }

    /// <summary>"Làm mới buổi học": đưa mọi học sinh về chưa điểm danh. Giữ dòng dữ liệu (không xóa cứng, D-16).</summary>
    public void Reset(Guid updatedBy, DateTime now)
    {
        foreach (var record in _records)
        {
            record.Apply(AttendanceEntry.Empty, updatedBy, now);
        }

        Topic = null;
        NextHomework = null;
        LevelComment1 = LevelComment2 = LevelComment3 = LevelComment4 = LevelComment5 = null;
        Touch(updatedBy, now);
    }

    private void Touch(Guid updatedBy, DateTime now)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    internal static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, $"Nội dung tối đa {maxLength} ký tự.");
        }

        return trimmed;
    }
}

/// <summary>Điểm danh + BTVN của một học sinh trong một buổi.</summary>
public sealed class AttendanceRecord : Entity
{
    public const int MaxNoteLength = 1000;
    public const decimal MaxScore = 10m;

    private AttendanceRecord()
    {
    }

    internal AttendanceRecord(Guid sessionId, Guid studentId)
    {
        SessionId = sessionId;
        StudentId = studentId;
    }

    public Guid SessionId { get; private set; }

    public Guid StudentId { get; private set; }

    public AttendanceStatus? Status { get; private set; }

    public HomeworkStatus? HomeworkStatus { get; private set; }

    /// <summary>Điểm BTVN thang 10, bước 0,5.</summary>
    public decimal? HomeworkScore { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Phân loại 1..5 trong buổi (null = không phân loại); dùng nhận xét chung của loại đó (D-47).</summary>
    public int? Level { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public static bool IsValidLevel(int level) => level is >= 1 and <= AttendanceSession.LevelCount;

    /// <summary>Điểm hợp lệ: 0–10, bội số của 0,5.</summary>
    public static bool IsValidScore(decimal score) => score is >= 0 and <= MaxScore && score * 2 == decimal.Truncate(score * 2);

    internal void Apply(AttendanceEntry entry, Guid updatedBy, DateTime now)
    {
        if (entry.HomeworkScore is { } score && !IsValidScore(score))
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, "Điểm BTVN từ 0 đến 10, bước 0,5.");
        }

        if (entry.Level is { } level && !IsValidLevel(level))
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, $"Phân loại từ 1 đến {AttendanceSession.LevelCount}.");
        }

        Status = entry.Status;
        Level = entry.Level;
        HomeworkStatus = entry.HomeworkStatus;
        HomeworkScore = entry.HomeworkScore;
        Note = AttendanceSession.Clean(entry.Note, MaxNoteLength);
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

public sealed record AttendanceEntry(
    AttendanceStatus? Status, HomeworkStatus? HomeworkStatus, decimal? HomeworkScore, string? Note, int? Level = null)
{
    public static AttendanceEntry Empty { get; } = new(null, null, null, null);
}

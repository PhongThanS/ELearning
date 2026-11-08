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

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Điểm hợp lệ: 0–10, bội số của 0,5.</summary>
    public static bool IsValidScore(decimal score) => score is >= 0 and <= MaxScore && score * 2 == decimal.Truncate(score * 2);

    internal void Apply(AttendanceEntry entry, Guid updatedBy, DateTime now)
    {
        if (entry.HomeworkScore is { } score && !IsValidScore(score))
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, "Điểm BTVN từ 0 đến 10, bước 0,5.");
        }

        Status = entry.Status;
        HomeworkStatus = entry.HomeworkStatus;
        HomeworkScore = entry.HomeworkScore;
        Note = AttendanceSession.Clean(entry.Note, MaxNoteLength);
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

public sealed record AttendanceEntry(AttendanceStatus? Status, HomeworkStatus? HomeworkStatus, decimal? HomeworkScore, string? Note)
{
    public static AttendanceEntry Empty { get; } = new(null, null, null, null);
}

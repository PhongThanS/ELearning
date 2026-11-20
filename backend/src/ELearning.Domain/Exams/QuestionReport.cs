using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Exams;

/// <summary>
/// Học sinh báo một câu hỏi bị lỗi sau khi đã nộp bài (D-46). Gắn với câu trong snapshot đề
/// (<see cref="ExamQuestionId"/>) của đúng lượt thi; mỗi lượt thi báo một câu tối đa một lần
/// (báo lại khi còn chờ xử lý thì cập nhật lý do). Không xóa cứng (D-16).
/// </summary>
public sealed class QuestionReport : Entity
{
    public const int MaxNoteLength = 1000;

    private QuestionReport()
    {
    }

    public QuestionReport(
        Guid attemptId, Guid examId, Guid examQuestionId, int questionOrder, Guid reportedBy,
        QuestionReportReason reason, string? note, DateTime now)
    {
        AttemptId = attemptId;
        ExamId = examId;
        ExamQuestionId = examQuestionId;
        QuestionOrder = questionOrder;
        ReportedBy = reportedBy;
        Reason = reason;
        Note = Clean(note);
        Status = QuestionReportStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid AttemptId { get; private set; }

    public Guid ExamId { get; private set; }

    /// <summary>Câu trong snapshot đề (không phải câu trong ngân hàng).</summary>
    public Guid ExamQuestionId { get; private set; }

    /// <summary>Số thứ tự câu mà học sinh nhìn thấy trong lượt thi của mình.</summary>
    public int QuestionOrder { get; private set; }

    public Guid ReportedBy { get; private set; }

    public QuestionReportReason Reason { get; private set; }

    public string? Note { get; private set; }

    public QuestionReportStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    public bool IsOpen => Status == QuestionReportStatus.Open;

    /// <summary>Học sinh sửa lý do khi báo lỗi còn chờ xử lý; đã xử lý thì không đổi được.</summary>
    public bool UpdateByReporter(QuestionReportReason reason, string? note, DateTime now)
    {
        if (!IsOpen)
        {
            return false;
        }

        Reason = reason;
        Note = Clean(note);
        UpdatedAt = now;
        return true;
    }

    /// <summary>Đóng báo lỗi: <see cref="QuestionReportStatus.Resolved"/> (lỗi thật) hoặc <see cref="QuestionReportStatus.Rejected"/>.</summary>
    public void Resolve(QuestionReportStatus status, Guid by, string? note, DateTime now)
    {
        if (status == QuestionReportStatus.Open)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Trạng thái xử lý phải là RESOLVED hoặc REJECTED.");
        }

        Status = status;
        ResolvedBy = by;
        ResolvedAt = now;
        ResolutionNote = Clean(note);
        UpdatedAt = now;
    }

    /// <summary>Mở lại báo lỗi đã xử lý (xử lý nhầm).</summary>
    public void Reopen(DateTime now)
    {
        Status = QuestionReportStatus.Open;
        ResolvedBy = null;
        ResolvedAt = null;
        ResolutionNote = null;
        UpdatedAt = now;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

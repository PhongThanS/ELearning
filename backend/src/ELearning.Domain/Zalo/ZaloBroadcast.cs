using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Zalo;

/// <summary>
/// Một đợt gửi tin Zalo của thầy / cô tới phụ huynh trong một lớp (D-45). Đợt không sửa được sau khi tạo;
/// chỉ các tin con thay đổi trạng thái. Không xóa cứng (D-16).
/// </summary>
public sealed class ZaloBroadcast : Entity
{
    public const int MaxTextLength = 400;

    private readonly List<ZaloMessage> _messages = [];

    private ZaloBroadcast()
    {
    }

    public ZaloBroadcast(
        Guid classroomId, ZaloMessageKind kind, DateOnly? sessionDate, string? content, Guid createdBy, DateTime createdAt)
    {
        ClassroomId = classroomId;
        Kind = kind;
        SessionDate = sessionDate;
        Content = content;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid ClassroomId { get; private set; }

    public ZaloMessageKind Kind { get; private set; }

    /// <summary>Ngày buổi học (chỉ với báo cáo học tập).</summary>
    public DateOnly? SessionDate { get; private set; }

    /// <summary>Nội dung thông báo (chỉ với thông báo chung).</summary>
    public string? Content { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<ZaloMessage> Messages => _messages;

    public ZaloMessage AddMessage(
        Guid studentId, string? recipientName, string phone, string templateId, string templateData, DateTime now)
    {
        var message = new ZaloMessage(Id, studentId, recipientName, phone, templateId, templateData, now);
        _messages.Add(message);
        return message;
    }

    /// <summary>Hủy các tin chưa gửi; trả số tin bị hủy. Tin đã gửi / đang gửi / lỗi giữ nguyên.</summary>
    public int CancelPending(DateTime now)
    {
        var count = 0;
        foreach (var message in _messages.Where(m => m.Status == ZaloMessageStatus.Pending))
        {
            message.Cancel();
            count++;
        }

        if (count > 0)
        {
            CancelledAt ??= now;
        }

        return count;
    }
}

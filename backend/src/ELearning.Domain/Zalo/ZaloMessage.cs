using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Zalo;

/// <summary>Một tin Zalo tới một phụ huynh (một học sinh) trong hàng đợi gửi (D-45).</summary>
public sealed class ZaloMessage : Entity
{
    public const int MaxPhoneLength = 15;
    public const int MaxErrorLength = 500;

    private ZaloMessage()
    {
    }

    internal ZaloMessage(
        Guid broadcastId, Guid studentId, string? recipientName, string phone, string templateId, string templateData, DateTime now)
    {
        BroadcastId = broadcastId;
        StudentId = studentId;
        RecipientName = recipientName;
        Phone = phone;
        TemplateId = templateId;
        TemplateData = templateData;
        TrackingId = Guid.NewGuid().ToString("N");
        Status = ZaloMessageStatus.Pending;
        CreatedAt = now;
    }

    public Guid BroadcastId { get; private set; }

    public Guid StudentId { get; private set; }

    /// <summary>Tên phụ huynh lúc gửi (bản chụp).</summary>
    public string? RecipientName { get; private set; }

    /// <summary>Số di động dạng quốc tế không có dấu +, ví dụ 84912345678.</summary>
    public string Phone { get; private set; } = null!;

    /// <summary>Mã mẫu tin lúc tạo (bản chụp, không đổi khi cấu hình đổi).</summary>
    public string TemplateId { get; private set; } = null!;

    /// <summary>JSON {tên tham số mẫu: giá trị} đã soạn sẵn lúc tạo.</summary>
    public string TemplateData { get; private set; } = null!;

    /// <summary>Mã theo dõi gửi cho Zalo; mỗi tin một mã.</summary>
    public string TrackingId { get; private set; } = null!;

    public ZaloMessageStatus Status { get; private set; }

    /// <summary>Số lần đã thử gửi (gồm cả lần "gửi lại" do người dùng bấm).</summary>
    public int Attempts { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? SendingAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void MarkSent(string? providerMessageId, DateTime now)
    {
        EnsureSending();
        Status = ZaloMessageStatus.Sent;
        SentAt = now;
        ProviderMessageId = Truncate(providerMessageId, 100);
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorCode, string? errorMessage)
    {
        EnsureSending();
        Status = ZaloMessageStatus.Failed;
        ErrorCode = Truncate(errorCode, 50);
        ErrorMessage = Truncate(errorMessage, MaxErrorLength);
    }

    /// <summary>Đưa tin lỗi về hàng đợi để gửi lại (người dùng bấm "Gửi lại").</summary>
    public void Requeue()
    {
        if (Status != ZaloMessageStatus.Failed)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Chỉ gửi lại được tin bị lỗi.");
        }

        Status = ZaloMessageStatus.Pending;
        SendingAt = null;
    }

    public void Cancel()
    {
        if (Status != ZaloMessageStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Chỉ hủy được tin chưa gửi.");
        }

        Status = ZaloMessageStatus.Cancelled;
    }

    private void EnsureSending()
    {
        if (Status != ZaloMessageStatus.Sending)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Tin chưa ở trạng thái đang gửi.");
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}

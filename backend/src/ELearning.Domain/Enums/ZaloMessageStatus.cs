namespace ELearning.Domain.Enums;

/// <summary>Trạng thái một tin Zalo trong hàng đợi gửi (D-45).</summary>
public enum ZaloMessageStatus
{
    Pending = 1,

    /// <summary>Đã được job nhận để gửi; nếu kẹt quá lâu thì chuyển Failed (không rõ kết quả).</summary>
    Sending = 2,

    Sent = 3,

    Failed = 4,

    Cancelled = 5,
}

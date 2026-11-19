namespace ELearning.Domain.Enums;

/// <summary>Loại tin Zalo gửi phụ huynh (D-45).</summary>
public enum ZaloMessageKind
{
    /// <summary>Báo cáo học tập của một buổi: mỗi phụ huynh một tin riêng về con mình.</summary>
    DailyReport = 1,

    /// <summary>Thông báo chung của thầy / cô gửi tới phụ huynh cả lớp.</summary>
    Announcement = 2,
}

using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Zalo;

/// <summary>
/// Gửi tin Zalo cho phụ huynh (D-45). Section "Zalo". Bí mật (SecretKey, RefreshToken) đặt bằng biến môi trường,
/// ví dụ <c>Zalo__SecretKey</c>; không commit vào appsettings.
/// </summary>
public sealed class ZaloOptions
{
    public const string SectionName = "Zalo";

    public const string ProviderDisabled = "Disabled";
    public const string ProviderLog = "Log";
    public const string ProviderZns = "Zns";

    /// <summary><c>Disabled</c> = tắt (mặc định); <c>Log</c> = chỉ ghi log, không gửi thật (thử trên máy dev); <c>Zns</c> = gửi thật qua Zalo Notification Service.</summary>
    public string Provider { get; init; } = ProviderDisabled;

    /// <summary>App ID của ứng dụng Zalo đã liên kết với Official Account.</summary>
    public string AppId { get; init; } = "";

    /// <summary>Secret key của ứng dụng Zalo (dùng đổi refresh token lấy access token).</summary>
    public string SecretKey { get; init; } = "";

    /// <summary>
    /// Refresh token OA lấy lần đầu (khởi tạo). Zalo cấp refresh token mới mỗi lần đổi nên hệ thống tự lưu bản mới vào DB;
    /// đổi giá trị này thì hệ thống dùng lại giá trị mới.
    /// </summary>
    public string RefreshToken { get; init; } = "";

    /// <summary>Đặt <c>development</c> để gửi thử trong chế độ phát triển của Zalo (tin chỉ tới tài khoản quản trị / thử nghiệm, không tính phí).</summary>
    public string? Mode { get; init; }

    /// <summary>Tắt job gửi nền trên instance này (test dùng).</summary>
    public bool WorkerEnabled { get; init; } = true;

    [Range(1, 600)]
    public int PollSeconds { get; init; } = 5;

    [Range(1, 200)]
    public int BatchSize { get; init; } = 50;

    /// <summary>Nghỉ giữa hai tin để không vượt giới hạn tốc độ của Zalo (mili-giây). 0 = không nghỉ.</summary>
    [Range(0, 5000)]
    public int SendIntervalMs { get; init; } = 250;

    /// <summary>Độ dài tối đa của mỗi tham số mẫu (Zalo giới hạn theo loại tham số, thường 30–200 ký tự).</summary>
    [Range(20, 500)]
    public int MaxParamLength { get; init; } = 200;

    /// <summary>Khóa: <c>DailyReport</c>, <c>Announcement</c>.</summary>
    public Dictionary<string, ZaloTemplateOptions> Templates { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => !string.Equals(Provider, ProviderDisabled, StringComparison.OrdinalIgnoreCase);

    public bool IsZns => string.Equals(Provider, ProviderZns, StringComparison.OrdinalIgnoreCase);
}

public sealed class ZaloTemplateOptions
{
    /// <summary>Mã mẫu ZNS đã được Zalo duyệt.</summary>
    public string TemplateId { get; init; } = "";

    /// <summary>
    /// Ánh xạ <c>tên tham số trong mẫu → trường dữ liệu của hệ thống</c>. Để trống = dùng đúng tên trường làm tên tham số.
    /// Trường có sẵn: parentName, studentName, className, date, attendance, score, topic, homework, deadline, attention, note, content.
    /// </summary>
    public Dictionary<string, string> Params { get; init; } = new(StringComparer.Ordinal);
}

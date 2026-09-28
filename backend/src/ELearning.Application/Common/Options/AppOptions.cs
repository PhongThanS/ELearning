using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Cấu hình chung của ứng dụng. Section "App".</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Múi giờ nghiệp vụ, dùng cho các khái niệm "hôm nay" trên dashboard.</summary>
    [Required]
    public string BusinessTimeZone { get; init; } = "Asia/Ho_Chi_Minh";

    /// <summary>Origin công khai của frontend (kiểm tra header Origin, CORS dự phòng).</summary>
    [Required]
    [Url]
    public string PublicOrigin { get; init; } = "http://localhost:5173";
}

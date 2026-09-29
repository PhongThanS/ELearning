using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Ảnh trong câu hỏi (D-27). Section "Media".</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Thư mục lưu file ảnh (Docker: bind mount MEDIA_DIR, docs/09-van-hanh.md mục 5.1).</summary>
    [Required]
    public string RootPath { get; init; } = string.Empty;

    /// <summary>Kích thước tối đa một ảnh.</summary>
    [Range(1024, 10 * 1024 * 1024)]
    public int MaxBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>
    /// Link ảnh hết hạn sau khoảng [LinkLifetimeHours, 2 × LinkLifetimeHours]: mốc hết hạn được làm tròn lên
    /// theo khối LinkLifetimeHours để cùng một ảnh có cùng URL trong cả khối, trình duyệt cache được.
    /// </summary>
    [Range(1, 24)]
    public int LinkLifetimeHours { get; init; } = 6;
}

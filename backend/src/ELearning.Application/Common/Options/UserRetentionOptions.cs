using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Tự động xóa tài khoản bị khóa lâu (D-41). Section "UserRetention".</summary>
public sealed class UserRetentionOptions
{
    public const string SectionName = "UserRetention";

    /// <summary>Tài khoản khóa quá số ngày này thì bị xóa cứng. 0 = tắt tính năng.</summary>
    [Range(0, 3650)]
    public int DeleteLockedAfterDays { get; init; } = 90;

    /// <summary>Chu kỳ quét (phút).</summary>
    [Range(1, 1440)]
    public int SweepIntervalMinutes { get; init; } = 360;

    /// <summary>Số tài khoản tối đa mỗi lần quét.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; init; } = 100;
}

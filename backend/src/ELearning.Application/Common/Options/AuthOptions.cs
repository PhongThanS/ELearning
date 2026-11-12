using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Section "Auth" (docs/07-bao-mat.md mục 2).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>D-10: mặc định tắt; Development bật trong appsettings.Development.json.</summary>
    public bool AllowSelfRegistration { get; init; }

    [Range(1, 50)]
    public int LockoutMaxFailedAttempts { get; init; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; init; } = 15;

    /// <summary>Khoảng thời gian coi việc dùng lại refresh token vừa xoay vòng là request song song hợp lệ (D-24).</summary>
    [Range(0, 120)]
    public int RefreshReuseGraceSeconds { get; init; } = 30;
}

/// <summary>Section "Jwt". SigningKey bắt buộc, tối thiểu 32 byte, lấy từ secret store.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; init; } = "ELearning";

    [Required]
    public string Audience { get; init; } = "ELearning.Web";

    [Required]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 7;

    /// <summary>
    /// Thời gian tối đa của một lần đăng nhập, tính từ lúc nhập mật khẩu (D-34). Làm mới token không kéo dài quá mốc này:
    /// hết giờ thì phải đăng nhập lại. 0 = không giới hạn (refresh token gia hạn trượt theo RefreshTokenDays như trước).
    /// </summary>
    [Range(0, 2160)]
    public int SessionMaxHours { get; init; } = 12;
}

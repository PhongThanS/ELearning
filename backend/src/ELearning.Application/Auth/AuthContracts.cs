using ELearning.Application.Common;
using FluentValidation;

namespace ELearning.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record RegisterRequest(string UserName, string Email, string FullName, string Password, bool AcceptTerms);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AuthUserDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);

/// <summary>Kết quả đăng nhập/refresh; RefreshToken được controller đặt vào cookie, không trả trong body.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTime ExpiresAt,
    AuthUserDto User,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

/// <summary>Response body của login/refresh/change-password (docs/05-api.md mục 6.1).</summary>
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUserDto User)
{
    public static AuthResponse From(AuthSession session) => new(session.AccessToken, session.ExpiresAt, session.User);
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.UserName).NotEmpty().WithErrorCode("USERNAME_REQUIRED").WithMessage("Vui lòng nhập tên đăng nhập.")
            .MaximumLength(255);
        RuleFor(r => r.Password).NotEmpty().WithErrorCode("PASSWORD_REQUIRED").WithMessage("Vui lòng nhập mật khẩu.")
            .MaximumLength(PasswordRules.MaxLength);
    }
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.UserName).ValidUserName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Password).StrongPassword()
            .Must((r, p) => !string.Equals(p, r.UserName, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("PASSWORD_EQUALS_USERNAME").WithMessage("Mật khẩu không được trùng tên đăng nhập.");
        RuleFor(r => r.AcceptTerms).Equal(true)
            .WithErrorCode("TERMS_NOT_ACCEPTED").WithMessage("Bạn cần đồng ý điều khoản và chính sách dữ liệu.");
    }
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().WithErrorCode("PASSWORD_REQUIRED")
            .WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(r => r.NewPassword).StrongPassword()
            .NotEqual(r => r.CurrentPassword).WithErrorCode("PASSWORD_UNCHANGED")
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

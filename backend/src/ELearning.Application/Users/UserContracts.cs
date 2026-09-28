using ELearning.Application.Common;
using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Users;

public sealed record UserListQuery : PageRequest
{
    public string? Keyword { get; init; }

    public string? RoleCode { get; init; }

    public Guid? GroupId { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record UserListItemDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    bool IsActive,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

public sealed record RefDto(Guid Id, string Code, string Name);

public sealed record UserDetailDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LockoutEnd,
    DateTime? LastLoginAt,
    DateTime? AnonymizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<RefDto> Roles,
    IReadOnlyList<RefDto> Groups,
    string RowVersion);

/// <summary>Mật khẩu tạm chỉ được trả về một lần, ngay trong response này.</summary>
public sealed record UserWithPasswordDto(UserDetailDto User, string? TemporaryPassword);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string FullName,
    string? Password,
    IReadOnlyList<Guid>? RoleIds,
    IReadOnlyList<Guid>? GroupIds);

public sealed record UpdateUserRequest(string Email, string FullName, string RowVersion);

public sealed record SetUserStatusRequest(bool IsActive, string? Reason);

public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record ReasonRequest(string Reason);

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.UserName).ValidUserName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.FullName).ValidFullName();
        When(r => r.Password is not null, () =>
            RuleFor(r => r.Password!).StrongPassword()
                .Must((r, p) => !string.Equals(p, r.UserName, StringComparison.OrdinalIgnoreCase))
                .WithErrorCode("PASSWORD_EQUALS_USERNAME").WithMessage("Mật khẩu không được trùng tên đăng nhập."));
    }
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

internal sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator() =>
        RuleFor(r => r.Reason).NotEmpty().WithErrorCode("REASON_REQUIRED").WithMessage("Vui lòng nhập lý do.")
            .MaximumLength(1000);
}

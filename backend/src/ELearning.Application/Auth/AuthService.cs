using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthUserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<Result<AuthSession>> RefreshAsync(string? rawRefreshToken, CancellationToken ct);

    Task LogoutAsync(string? rawRefreshToken, CancellationToken ct);

    Task<Result<AuthUserDto>> GetMeAsync(Guid userId, CancellationToken ct);

    Task<Result<AuthSession>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
}

/// <summary>Xác thực: docs/07-bao-mat.md mục 2–3, docs/05-api.md mục 6.1.</summary>
internal sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUserAccessService userAccess,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IOptions<AuthOptions> authOptions,
    IOptions<JwtOptions> jwtOptions,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator) : IAuthService
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized(ErrorCodes.InvalidCredentials, "Tên đăng nhập hoặc mật khẩu không đúng.");

    private static readonly Error InvalidToken =
        Error.Unauthorized(ErrorCodes.TokenInvalid, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

    private static readonly Error AccountLocked =
        Error.Unauthorized(ErrorCodes.AccountLocked, "Tài khoản tạm bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");

    private readonly AuthOptions _auth = authOptions.Value;
    private readonly JwtOptions _jwt = jwtOptions.Value;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<Result<AuthUserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!_auth.AllowSelfRegistration)
        {
            return new Error(ErrorType.Forbidden, ErrorCodes.RegistrationDisabled, "Chức năng tự đăng ký đang tắt.");
        }

        var errors = await registerValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AuthUserDto>.Failure(errors);
        }

        if (await db.FindDuplicateUserAsync(request.UserName, request.Email, null, ct) is { } duplicate)
        {
            return duplicate;
        }

        var studentRole = await db.Roles.SingleAsync(r => r.Code == Role.StudentCode, ct);
        var user = new User(request.UserName, request.Email, request.FullName, Now);
        user.SetPassword(passwordHasher.Hash(user, request.Password), mustChangePassword: false, Now);
        user.SetRoles([studentRole.Id], Now);
        db.Users.Add(user);
        audit.Write(AuditActions.UserRegister, nameof(User), user.Id, newValue: new { user.UserName, user.Email }, userId: user.Id);
        await db.SaveChangesAsync(ct);

        return await BuildUserDtoAsync(user, ct);
    }

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var errors = await loginValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AuthSession>.Failure(errors);
        }

        var key = User.NormalizeKey(request.UserName);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == key || u.NormalizedEmail == key, ct);

        if (user is null || user.IsAnonymized || user.PasswordHash is null)
        {
            audit.Write(AuditActions.UserLoginFailed, newValue: new { UserName = Truncate(request.UserName, 100) });
            await db.SaveChangesAsync(ct);
            return InvalidCredentials;
        }

        if (user.IsLockedOut(Now))
        {
            return AccountLocked;
        }

        var check = passwordHasher.Verify(user, request.Password);
        if (check == PasswordCheck.Failed)
        {
            var locked = user.RecordFailedLogin(Now, _auth.LockoutMaxFailedAttempts, TimeSpan.FromMinutes(_auth.LockoutMinutes));
            audit.Write(AuditActions.UserLoginFailed, nameof(User), user.Id, userId: user.Id);
            if (locked)
            {
                audit.Write(AuditActions.UserLockedOut, nameof(User), user.Id, userId: user.Id);
            }

            await db.SaveChangesAsync(ct);
            return locked ? AccountLocked : InvalidCredentials;
        }

        // Chỉ báo "đã vô hiệu hóa" khi mật khẩu đúng, để không lộ thông tin tài khoản.
        if (!user.IsActive)
        {
            return Error.Unauthorized(ErrorCodes.AccountDisabled, "Tài khoản đã bị vô hiệu hóa.");
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.SetPassword(passwordHasher.Hash(user, request.Password), user.MustChangePassword, Now);
        }

        user.RecordSuccessfulLogin(Now);
        var (session, _) = await IssueSessionAsync(user, Guid.NewGuid(), ct);
        audit.Write(AuditActions.UserLogin, nameof(User), user.Id, userId: user.Id);
        await db.SaveChangesAsync(ct);
        userAccess.Invalidate(user.Id);
        return session;
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return InvalidToken;
        }

        var hash = tokenService.HashRefreshToken(rawRefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.ExpiresAt <= Now)
        {
            return InvalidToken;
        }

        if (token.IsRevoked && !IsConcurrentRefresh(token))
        {
            // Token đã xoay vòng mà vẫn bị gửi lại: có thể đã bị đánh cắp → thu hồi cả chuỗi (D-15).
            if (token.RevokedReason == RefreshTokenRevokedReason.Rotated)
            {
                var now = Now;
                var family = await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null).ToListAsync(ct);
                family.ForEach(t => t.Revoke(RefreshTokenRevokedReason.ReuseDetected, now));
                audit.Write(AuditActions.RefreshTokenReuse, nameof(RefreshToken), token.Id, userId: token.UserId);
                await db.SaveChangesAsync(ct);
            }

            return InvalidToken;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive || user.IsAnonymized)
        {
            return InvalidToken;
        }

        var (session, replacement) = await IssueSessionAsync(user, token.FamilyId, ct);
        token.Revoke(RefreshTokenRevokedReason.Rotated, Now, replacement.Id);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        var hash = tokenService.HashRefreshToken(rawRefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.IsRevoked)
        {
            return;
        }

        token.Revoke(RefreshTokenRevokedReason.Logout, Now);
        audit.Write(AuditActions.UserLogout, nameof(User), token.UserId, userId: token.UserId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<AuthUserDto>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? Error.NotFound() : await BuildUserDtoAsync(user, ct);
    }

    public async Task<Result<AuthSession>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var errors = await changePasswordValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<AuthSession>.Failure(errors);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return InvalidToken;
        }

        if (passwordHasher.Verify(user, request.CurrentPassword) == PasswordCheck.Failed)
        {
            return Error.Business(ErrorCodes.InvalidCurrentPassword, "Mật khẩu hiện tại không đúng.");
        }

        if (string.Equals(request.NewPassword, user.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("PASSWORD_EQUALS_USERNAME", "Mật khẩu không được trùng tên đăng nhập.", "newPassword");
        }

        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), mustChangePassword: false, Now);
        await db.RevokeAllRefreshTokensAsync(user.Id, RefreshTokenRevokedReason.PasswordChanged, Now, ct);
        var (session, _) = await IssueSessionAsync(user, Guid.NewGuid(), ct);
        audit.Write(AuditActions.PasswordChanged, nameof(User), user.Id);
        await db.SaveChangesAsync(ct);
        userAccess.Invalidate(user.Id);
        return session;
    }

    /// <summary>D-24: dùng lại token vừa xoay vòng trong vài giây = hai tab refresh cùng lúc, không phải bị đánh cắp.</summary>
    private bool IsConcurrentRefresh(RefreshToken token) =>
        token.RevokedReason == RefreshTokenRevokedReason.Rotated
        && token.RevokedAt >= Now.AddSeconds(-_auth.RefreshReuseGraceSeconds);

    private async Task<(AuthSession Session, RefreshToken Entity)> IssueSessionAsync(User user, Guid familyId, CancellationToken ct)
    {
        var now = Now;
        var refresh = tokenService.GenerateRefreshToken();
        var entity = new RefreshToken(
            user.Id,
            familyId,
            refresh.TokenHash,
            now,
            now.AddDays(_jwt.RefreshTokenDays),
            currentUser.IpAddress,
            Truncate(currentUser.UserAgent, 500));
        db.RefreshTokens.Add(entity);

        var dto = await BuildUserDtoAsync(user, ct);
        var access = tokenService.CreateAccessToken(user, dto.Roles);
        return (new AuthSession(access.Token, access.ExpiresAt, dto, refresh.RawToken, entity.ExpiresAt), entity);
    }

    private async Task<AuthUserDto> BuildUserDtoAsync(User user, CancellationToken ct)
    {
        var (roles, permissions) = await db.LoadRolesAndPermissionsAsync(user.Id, ct);
        return new AuthUserDto(user.Id, user.UserName, user.Email, user.FullName, roles, permissions, user.MustChangePassword);
    }

    private static string? Truncate(string? value, int max) => value is not null && value.Length > max ? value[..max] : value;
}

using ELearning.Domain.Identity;

namespace ELearning.Application.Common.Abstractions;

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheck Verify(User user, string password);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> roleCodes);

    GeneratedRefreshToken GenerateRefreshToken();

    string HashRefreshToken(string rawToken);
}

/// <summary>Ảnh chụp quyền của user, được cache ngắn hạn (docs/07-bao-mat.md mục 3.4, 4).</summary>
public sealed record UserAccessSnapshot(
    Guid UserId,
    bool IsActive,
    Guid SecurityStamp,
    bool MustChangePassword,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions);

public interface IUserAccessService
{
    Task<UserAccessSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    void Invalidate(Guid userId);

    /// <summary>Xóa cache của mọi user (khi quyền của một vai trò thay đổi).</summary>
    void InvalidateAll();
}

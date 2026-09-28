using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Identity;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        string? createdByIp,
        string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid UserId { get; private set; }

    /// <summary>Mọi token sinh ra từ một lần đăng nhập; dùng để thu hồi cả chuỗi khi phát hiện dùng lại.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>SHA-256 hex của token; DB không bao giờ lưu token gốc.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public RefreshTokenRevokedReason? RevokedReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }
}

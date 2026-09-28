namespace ELearning.Domain.Enums;

/// <summary>Lý do thu hồi refresh token.</summary>
public enum RefreshTokenRevokedReason
{
    Rotated = 1,
    Logout = 2,
    ReuseDetected = 3,
    PasswordChanged = 4,
    UserDisabled = 5,
    Admin = 6,
}

using System.Text;
using ELearning.Domain.Common;

namespace ELearning.Domain.Identity;

public sealed class User : Entity, IHasRowVersion
{
    private readonly List<UserRole> _roles = [];

    private User()
    {
    }

    public User(string userName, string email, string fullName, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        UserName = userName.Trim();
        NormalizedUserName = NormalizeKey(UserName);
        Email = email.Trim();
        NormalizedEmail = NormalizeKey(Email);
        FullName = fullName.Trim();
        IsActive = true;
        SecurityStamp = Guid.NewGuid();
        CreatedAt = createdAt;
    }

    public string UserName { get; private set; } = null!;

    public string NormalizedUserName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public bool EmailConfirmed { get; private set; }

    public string? PasswordHash { get; private set; }

    public Guid SecurityStamp { get; private set; }

    public string FullName { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public bool MustChangePassword { get; private set; }

    public int AccessFailedCount { get; private set; }

    public DateTime? LockoutEnd { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public DateTime? AnonymizedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<UserRole> Roles => _roles;

    public bool IsAnonymized => AnonymizedAt.HasValue;

    public bool IsLockedOut(DateTime now) => LockoutEnd.HasValue && LockoutEnd.Value > now;

    /// <summary>Đặt mật khẩu mới; đổi SecurityStamp để vô hiệu hóa token cũ.</summary>
    public void SetPassword(string passwordHash, bool mustChangePassword, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        EnsureNotAnonymized();
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        AccessFailedCount = 0;
        LockoutEnd = null;
        RotateSecurityStamp(now);
    }

    /// <summary>Ghi nhận đăng nhập sai; trả về true nếu lần này làm tài khoản bị khóa.</summary>
    public bool RecordFailedLogin(DateTime now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        if (AccessFailedCount < maxFailedAttempts)
        {
            return false;
        }

        AccessFailedCount = 0;
        LockoutEnd = now.Add(lockoutDuration);
        return true;
    }

    public void RecordSuccessfulLogin(DateTime now)
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
        LastLoginAt = now;
    }

    public void UpdateProfile(string email, string fullName, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        EnsureNotAnonymized();
        Email = email.Trim();
        NormalizedEmail = NormalizeKey(Email);
        FullName = fullName.Trim();
        UpdatedAt = now;
    }

    public void SetActive(bool isActive, DateTime now)
    {
        if (isActive)
        {
            EnsureNotAnonymized();
        }

        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        RotateSecurityStamp(now);
    }

    /// <summary>Thay toàn bộ vai trò; đổi SecurityStamp để quyền mới có hiệu lực ngay.</summary>
    public void SetRoles(IEnumerable<Guid> roleIds, DateTime now)
    {
        var target = roleIds.Distinct().ToHashSet();
        _roles.RemoveAll(r => !target.Contains(r.RoleId));
        foreach (var roleId in target.Where(id => _roles.TrueForAll(r => r.RoleId != id)))
        {
            _roles.Add(new UserRole(Id, roleId));
        }

        RotateSecurityStamp(now);
    }

    /// <summary>Ẩn danh hóa theo yêu cầu xóa dữ liệu cá nhân (docs/07-bao-mat.md mục 11).</summary>
    public void Anonymize(DateTime now)
    {
        EnsureNotAnonymized();
        var placeholder = $"deleted-{Id:N}";
        UserName = placeholder;
        NormalizedUserName = NormalizeKey(placeholder);
        Email = $"{placeholder}@deleted.local";
        NormalizedEmail = NormalizeKey(Email);
        FullName = "Người dùng đã xóa";
        PasswordHash = null;
        IsActive = false;
        EmailConfirmed = false;
        MustChangePassword = false;
        LastLoginAt = null;
        AnonymizedAt = now;
        RotateSecurityStamp(now);
    }

    private void RotateSecurityStamp(DateTime now)
    {
        SecurityStamp = Guid.NewGuid();
        UpdatedAt = now;
    }

    private void EnsureNotAnonymized()
    {
        if (IsAnonymized)
        {
            throw new DomainException(DomainErrorCodes.UserAnonymized, "Người dùng đã bị ẩn danh hóa.");
        }
    }

    /// <summary>Khóa tra cứu username/email: trim, NFC, UPPER invariant (không phụ thuộc collation).</summary>
    public static string NormalizeKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;
}

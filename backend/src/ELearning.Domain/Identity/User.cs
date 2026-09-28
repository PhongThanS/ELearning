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

using ELearning.Domain.Common;

namespace ELearning.Domain.Identity;

public sealed class Role : Entity
{
    public const string AdminCode = "ADMIN";
    public const string StudentCode = "STUDENT";

    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    public Role(string code, string name, bool isSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsSystem = isSystem;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions;
}

public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;
}

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public Permission Permission { get; private set; } = null!;
}

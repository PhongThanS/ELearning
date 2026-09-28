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

    public void Update(string name, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (IsSystem && !isActive)
        {
            throw new DomainException(DomainErrorCodes.SystemRoleImmutable, "Không thể vô hiệu hóa vai trò hệ thống.");
        }

        Name = name.Trim();
        IsActive = isActive;
    }

    public void SetPermissions(IEnumerable<Guid> permissionIds)
    {
        var target = permissionIds.Distinct().ToHashSet();
        _permissions.RemoveAll(p => !target.Contains(p.PermissionId));
        foreach (var permissionId in target.Where(id => _permissions.TrueForAll(p => p.PermissionId != id)))
        {
            _permissions.Add(new RolePermission(Id, permissionId));
        }
    }
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

using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Roles;

public sealed record RoleDto(Guid Id, string Code, string Name, bool IsSystem, bool IsActive, IReadOnlyList<string> Permissions);

public sealed record PermissionDto(Guid Id, string Code, string Name);

public sealed record CreateRoleRequest(string Code, string Name, IReadOnlyList<Guid>? PermissionIds);

public sealed record UpdateRoleRequest(string Name, bool IsActive);

public sealed record SetRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct);

    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct);

    Task<Result<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct);

    Task<Result<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct);

    Task<Result<RoleDto>> SetPermissionsAsync(Guid id, SetRolePermissionsRequest request, CancellationToken ct);
}

internal sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().WithErrorCode("CODE_REQUIRED").WithMessage("Vui lòng nhập mã vai trò.")
            .Matches("^[A-Za-z][A-Za-z0-9_]{1,49}$").WithErrorCode("CODE_INVALID")
            .WithMessage("Mã vai trò gồm 2–50 ký tự: chữ không dấu, số, gạch dưới; bắt đầu bằng chữ.");
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên vai trò.").MaximumLength(100);
    }
}

internal sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator() =>
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên vai trò.").MaximumLength(100);
}

/// <summary>Vai trò và permission (docs/07-bao-mat.md mục 4). Vai trò ADMIN luôn có mọi permission.</summary>
internal sealed class RoleService(
    IAppDbContext db,
    IUserAccessService userAccess,
    IAuditService audit,
    IValidator<CreateRoleRequest> createValidator,
    IValidator<UpdateRoleRequest> updateValidator) : IRoleService
{
    private static readonly Error RoleNotFound = Error.NotFound(message: "Không tìm thấy vai trò.");

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct) =>
        await db.Roles.AsNoTracking().OrderBy(r => r.Code)
            .Select(r => new RoleDto(
                r.Id,
                r.Code,
                r.Name,
                r.IsSystem,
                r.IsActive,
                r.Permissions.Select(p => p.Permission.Code).OrderBy(c => c).ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct) =>
        await db.Permissions.AsNoTracking().OrderBy(p => p.Code).Select(p => new PermissionDto(p.Id, p.Code, p.Name)).ToListAsync(ct);

    public async Task<Result<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<RoleDto>.Failure(errors);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.Code == code, ct))
        {
            return Error.Conflict(ErrorCodes.DuplicateCode, "Mã vai trò đã tồn tại.");
        }

        var permissionIds = request.PermissionIds?.Distinct().ToList() ?? [];
        if (await db.Permissions.CountAsync(p => permissionIds.Contains(p.Id), ct) != permissionIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có permission không tồn tại.", "permissionIds");
        }

        var role = new Role(code, request.Name, isSystem: false);
        role.SetPermissions(permissionIds);
        db.Roles.Add(role);
        audit.Write(AuditActions.RoleCreated, nameof(Role), role.Id, newValue: new { role.Code, role.Name, PermissionIds = permissionIds });
        await db.SaveChangesAsync(ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task<Result<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<RoleDto>.Failure(errors);
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return RoleNotFound;
        }

        var old = new { role.Name, role.IsActive };
        role.Update(request.Name, request.IsActive);
        audit.Write(AuditActions.RoleUpdated, nameof(Role), role.Id, old, new { role.Name, role.IsActive });
        await db.SaveChangesAsync(ct);
        userAccess.InvalidateAll();
        return await GetAsync(id, ct);
    }

    public async Task<Result<RoleDto>> SetPermissionsAsync(Guid id, SetRolePermissionsRequest request, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return RoleNotFound;
        }

        if (role.Code == Role.AdminCode)
        {
            return Error.Conflict(ErrorCodes.SystemRoleImmutable, "Vai trò ADMIN luôn có mọi quyền, không thể thay đổi.");
        }

        var permissionIds = request.PermissionIds.Distinct().ToList();
        var permissions = await db.Permissions.Where(p => permissionIds.Contains(p.Id)).ToListAsync(ct);
        if (permissions.Count != permissionIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có permission không tồn tại.", "permissionIds");
        }

        var oldCodes = role.Permissions.Select(p => p.Permission.Code).Order(StringComparer.Ordinal).ToList();
        role.SetPermissions(permissionIds);
        audit.Write(
            AuditActions.RolePermissionsChanged,
            nameof(Role),
            role.Id,
            new { Permissions = oldCodes },
            new { Permissions = permissions.Select(p => p.Code).Order(StringComparer.Ordinal).ToList() });
        await db.SaveChangesAsync(ct);
        userAccess.InvalidateAll();
        return await GetAsync(id, ct);
    }

    private async Task<Result<RoleDto>> GetAsync(Guid id, CancellationToken ct) =>
        (await ListRolesAsync(ct)).Single(r => r.Id == id);
}

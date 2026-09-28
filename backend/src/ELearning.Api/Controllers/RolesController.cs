using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Roles;
using ELearning.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Vai trò và permission (docs/05-api.md mục 6.2).</summary>
[Route("api")]
public sealed class RolesController(IRoleService roles) : ApiControllerBase
{
    [HttpGet("roles")]
    [HasPermission(Permissions.RoleView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RoleDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListRoles(CancellationToken ct) => Ok(ApiResponse.Ok(await roles.ListRolesAsync(ct), TraceId));

    [HttpGet("permissions")]
    [HasPermission(Permissions.RoleView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PermissionDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListPermissions(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await roles.ListPermissionsAsync(ct), TraceId));

    [HttpPost("roles")]
    [HasPermission(Permissions.RoleManage)]
    [ProducesResponseType<ApiResponse<RoleDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateRoleRequest request, CancellationToken ct) =>
        ToResponse(await roles.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("roles/{id:guid}")]
    [HasPermission(Permissions.RoleManage)]
    [ProducesResponseType<ApiResponse<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateRoleRequest request, CancellationToken ct) =>
        ToResponse(await roles.UpdateAsync(id, request, ct));

    [HttpPut("roles/{id:guid}/permissions")]
    [HasPermission(Permissions.RoleManage)]
    [ProducesResponseType<ApiResponse<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetPermissions(Guid id, SetRolePermissionsRequest request, CancellationToken ct) =>
        ToResponse(await roles.SetPermissionsAsync(id, request, ct));
}

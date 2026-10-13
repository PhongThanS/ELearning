using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Auth;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Users;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Quản trị người dùng (docs/05-api.md mục 6.2).</summary>
[Route("api/users")]
public sealed class UsersController(
    IUserService users,
    IRegistrationSettingService registrationSetting,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("registration-setting")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<RegistrationStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRegistrationSetting(CancellationToken ct) =>
        Ok(ApiResponse.Ok(new RegistrationStatusDto(await registrationSetting.IsSelfRegistrationAllowedAsync(ct)), TraceId));

    [HttpPatch("registration-setting")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<RegistrationStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetRegistrationSetting([FromBody] SetRegistrationSettingRequest request, CancellationToken ct)
    {
        var result = await registrationSetting.SetSelfRegistrationAllowedAsync(request.AllowSelfRegistration, currentUser.UserId, ct);
        return Ok(ApiResponse.Ok(new RegistrationStatusDto(result), TraceId));
    }

    [HttpGet]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<PagedResult<UserListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await users.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await users.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.UserCreate)]
    [ProducesResponseType<ApiResponse<UserWithPasswordDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateUserRequest request, CancellationToken ct) =>
        ToResponse(await users.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        ToResponse(await users.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken ct) =>
        ToResponse(await users.SetStatusAsync(id, request, ct));

    [HttpPut("{id:guid}/roles")]
    [HasPermission(Permissions.RoleAssign)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetRoles(Guid id, SetUserRolesRequest request, CancellationToken ct) =>
        ToResponse(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(Permissions.UserResetPassword)]
    [ProducesResponseType<ApiResponse<UserWithPasswordDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResetPassword(Guid id, CancellationToken ct) =>
        ToResponse(await users.ResetPasswordAsync(id, ct));

    [HttpPost("{id:guid}/anonymize")]
    [HasPermission(Permissions.UserAnonymize)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Anonymize(Guid id, ReasonRequest request, CancellationToken ct) =>
        ToResponse(await users.AnonymizeAsync(id, request, ct));
}

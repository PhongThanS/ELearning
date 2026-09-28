using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Groups;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Nhóm người dùng (docs/05-api.md mục 6.2).</summary>
[Route("api/groups")]
public sealed class GroupsController(IGroupService groups) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.GroupView)]
    [ProducesResponseType<ApiResponse<PagedResult<GroupDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] GroupListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await groups.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.GroupView)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await groups.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.GroupManage)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateGroupRequest request, CancellationToken ct) =>
        ToResponse(await groups.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.GroupManage)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateGroupRequest request, CancellationToken ct) =>
        ToResponse(await groups.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.GroupManage)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetStatusRequest request, CancellationToken ct) =>
        ToResponse(await groups.SetStatusAsync(id, request, ct));

    [HttpGet("{id:guid}/members")]
    [HasPermission(Permissions.GroupView)]
    [ProducesResponseType<ApiResponse<PagedResult<GroupMemberDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Members(Guid id, [FromQuery] UserMemberQuery query, CancellationToken ct) =>
        ToResponse(await groups.ListMembersAsync(id, query, ct));

    [HttpPost("{id:guid}/members")]
    [HasPermission(Permissions.GroupManage)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AddMembers(Guid id, AddGroupMembersRequest request, CancellationToken ct) =>
        ToResponse(await groups.AddMembersAsync(id, request, ct));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [HasPermission(Permissions.GroupManage)]
    [ProducesResponseType<ApiResponse<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct) =>
        ToResponse(await groups.RemoveMemberAsync(id, userId, ct));
}

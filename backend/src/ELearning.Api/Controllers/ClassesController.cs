using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Classes;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Lớp học và học viên của lớp (D-28, docs/05-api.md). Không có DELETE (D-16): ngừng dùng thì tắt lớp.</summary>
[Route("api/classes")]
public sealed class ClassesController(IClassroomService classrooms) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClassView)]
    [ProducesResponseType<ApiResponse<PagedResult<ClassroomDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ClassroomListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await classrooms.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.ClassView)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await classrooms.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.ClassManage)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateClassroomRequest request, CancellationToken ct) =>
        ToResponse(await classrooms.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ClassManage)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateClassroomRequest request, CancellationToken ct) =>
        ToResponse(await classrooms.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.ClassManage)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetClassroomStatusRequest request, CancellationToken ct) =>
        ToResponse(await classrooms.SetStatusAsync(id, request, ct));

    [HttpGet("{id:guid}/students")]
    [HasPermission(Permissions.ClassView)]
    [ProducesResponseType<ApiResponse<PagedResult<ClassroomStudentDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Students(Guid id, [FromQuery] ClassroomStudentQuery query, CancellationToken ct) =>
        ToResponse(await classrooms.ListStudentsAsync(id, query, ct));

    [HttpPost("{id:guid}/students")]
    [HasPermission(Permissions.ClassManage)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AddStudents(Guid id, AddClassroomStudentsRequest request, CancellationToken ct) =>
        ToResponse(await classrooms.AddStudentsAsync(id, request, ct));

    [HttpDelete("{id:guid}/students/{userId:guid}")]
    [HasPermission(Permissions.ClassManage)]
    [ProducesResponseType<ApiResponse<ClassroomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveStudent(Guid id, Guid userId, CancellationToken ct) =>
        ToResponse(await classrooms.RemoveStudentAsync(id, userId, ct));
}

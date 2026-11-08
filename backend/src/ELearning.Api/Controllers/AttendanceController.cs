using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Attendance;
using ELearning.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>
/// Điểm danh + BTVN theo buổi (D-31, docs/05-api.md). Mọi endpoint cần Attendance.Take; lớp không do mình chủ nhiệm
/// (và không có Attendance.ManageAll) → 404. Ngày dạng yyyy-MM-dd theo lịch nghiệp vụ.
/// </summary>
[Route("api/attendance")]
[HasPermission(Permissions.AttendanceTake)]
public sealed class AttendanceController(IAttendanceService attendance) : ApiControllerBase
{
    [HttpGet("classes")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AttendanceClassDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Classes([FromQuery] AttendanceClassQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await attendance.ListClassesAsync(query, ct), TraceId));

    [HttpGet("classes/{classId:guid}/sessions/{date}")]
    [ProducesResponseType<ApiResponse<AttendanceSessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Session(Guid classId, DateOnly date, CancellationToken ct) =>
        ToResponse(await attendance.GetSessionAsync(classId, date, ct));

    [HttpPut("classes/{classId:guid}/sessions/{date}")]
    [ProducesResponseType<ApiResponse<SaveAttendanceResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Save(Guid classId, DateOnly date, SaveAttendanceRequest request, CancellationToken ct) =>
        ToResponse(await attendance.SaveSessionAsync(classId, date, request, ct));

    [HttpPost("classes/{classId:guid}/sessions/{date}/reset")]
    [ProducesResponseType<ApiResponse<AttendanceSessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reset(Guid classId, DateOnly date, CancellationToken ct) =>
        ToResponse(await attendance.ResetSessionAsync(classId, date, ct));

    [HttpGet("classes/{classId:guid}/sessions/{date}/export")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Export(Guid classId, DateOnly date, CancellationToken ct)
    {
        var result = await attendance.ExportAsync(classId, date, ct);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Failure(result);
    }

    [HttpPut("classes/{classId:guid}/students/{studentId:guid}/contact")]
    [ProducesResponseType<ApiResponse<StudentContactDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateContact(Guid classId, Guid studentId, UpdateStudentContactRequest request, CancellationToken ct) =>
        ToResponse(await attendance.UpdateContactAsync(classId, studentId, request, ct));

    [HttpGet("classes/{classId:guid}/students/{studentId:guid}/history")]
    [ProducesResponseType<ApiResponse<AttendanceStudentHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> History(Guid classId, Guid studentId, [FromQuery] AttendanceHistoryQuery query, CancellationToken ct) =>
        ToResponse(await attendance.GetStudentHistoryAsync(classId, studentId, query, ct));
}

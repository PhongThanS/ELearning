using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Zalo;
using ELearning.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>
/// Gửi tin Zalo cho phụ huynh cả lớp (D-45, docs/05-api.md). Cần Zalo.Send; lớp không do mình chủ nhiệm
/// (và không có Attendance.ManageAll) → 404.
/// </summary>
[Route("api/zalo")]
[HasPermission(Permissions.ZaloSend)]
public sealed class ZaloController(IZaloBroadcastService zalo) : ApiControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<ApiResponse<ZaloStatusDto>>(StatusCodes.Status200OK)]
    public ActionResult Status() => Ok(ApiResponse.Ok(zalo.GetStatus(), TraceId));

    [HttpPost("classes/{classId:guid}/preview")]
    [ProducesResponseType<ApiResponse<ZaloPreviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Preview(Guid classId, ZaloBroadcastRequest request, CancellationToken ct) =>
        ToResponse(await zalo.PreviewAsync(classId, request, ct));

    [HttpGet("classes/{classId:guid}/broadcasts")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ZaloBroadcastDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(Guid classId, CancellationToken ct) =>
        ToResponse(await zalo.ListAsync(classId, ct));

    [HttpPost("classes/{classId:guid}/broadcasts")]
    [ProducesResponseType<ApiResponse<ZaloBroadcastCreatedDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(Guid classId, ZaloBroadcastRequest request, CancellationToken ct) =>
        ToResponse(await zalo.CreateAsync(classId, request, ct), StatusCodes.Status201Created);

    [HttpGet("classes/{classId:guid}/broadcasts/{broadcastId:guid}")]
    [ProducesResponseType<ApiResponse<ZaloBroadcastDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid classId, Guid broadcastId, CancellationToken ct) =>
        ToResponse(await zalo.GetAsync(classId, broadcastId, ct));

    [HttpPost("classes/{classId:guid}/broadcasts/{broadcastId:guid}/retry")]
    [ProducesResponseType<ApiResponse<ZaloBroadcastDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Retry(Guid classId, Guid broadcastId, CancellationToken ct) =>
        ToResponse(await zalo.RetryFailedAsync(classId, broadcastId, ct));

    [HttpPost("classes/{classId:guid}/broadcasts/{broadcastId:guid}/cancel")]
    [ProducesResponseType<ApiResponse<ZaloBroadcastDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Cancel(Guid classId, Guid broadcastId, CancellationToken ct) =>
        ToResponse(await zalo.CancelAsync(classId, broadcastId, ct));
}

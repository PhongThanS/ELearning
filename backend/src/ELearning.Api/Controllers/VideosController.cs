using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Videos;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

[Route("api/videos")]
public sealed class VideosController(IVideoService videos) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.VideoView)]
    [ProducesResponseType<ApiResponse<PagedResult<VideoLessonDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] VideoListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await videos.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.VideoView)]
    [ProducesResponseType<ApiResponse<VideoLessonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) =>
        ToResponse(await videos.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType<ApiResponse<VideoLessonDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateVideoRequest request, CancellationToken ct) =>
        ToResponse(await videos.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType<ApiResponse<VideoLessonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateVideoRequest request, CancellationToken ct) =>
        ToResponse(await videos.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await videos.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType<ApiResponse<VideoLessonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, [FromBody] SetVideoStatusRequest request, CancellationToken ct) =>
        ToResponse(await videos.SetStatusAsync(id, request.IsActive, ct));
}

public sealed record SetVideoStatusRequest(bool IsActive);

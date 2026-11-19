using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Videos;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

[Route("api/videos")]
public sealed class VideosController(IVideoService videos, IVideoImportService imports) : ApiControllerBase
{
    private const long MaxImportBytes = 5 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>File mẫu import video (D-44): kèm hướng dẫn, mã lớp, mã chuyên đề, mã đề luyện.</summary>
    [HttpGet("import/template")]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ImportTemplate(CancellationToken ct) =>
        File(await imports.CreateTemplateAsync(ct), XlsxContentType, "mau-import-video.xlsx");

    /// <summary>Thêm video từ .xlsx (tối đa 5 MB, 200 video). dryRun=true chỉ kiểm tra. Tất cả hoặc không.</summary>
    [HttpPost("import")]
    [HasPermission(Permissions.VideoManage)]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<VideoImportResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Import(IFormFile? file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Failure(Result.Failure(Error.Validation("IMPORT_FILE_REQUIRED", "Vui lòng chọn file Excel.", "file")));
        }

        if (file.Length > MaxImportBytes || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(Result.Failure(Error.Validation("IMPORT_FILE_INVALID", "Chỉ nhận file .xlsx, tối đa 5 MB.", "file")));
        }

        await using var stream = file.OpenReadStream();
        return ToResponse(await imports.ImportAsync(stream, dryRun, ct));
    }

    [HttpGet]
    [HasPermission(Permissions.VideoView)]
    [ProducesResponseType<ApiResponse<PagedResult<VideoLessonDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] VideoListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await videos.ListAsync(query, ct), TraceId));

    /// <summary>Tiêu đề và ảnh bìa của video YouTube (oEmbed) để tự điền form bài giảng. Không lưu gì.</summary>
    [HttpGet("youtube-info")]
    [HasPermission(Permissions.VideoManage)]
    [ProducesResponseType<ApiResponse<YoutubeVideoInfo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> YoutubeInfo([FromQuery] string? url, CancellationToken ct) =>
        ToResponse(await videos.GetYoutubeInfoAsync(url, ct));

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

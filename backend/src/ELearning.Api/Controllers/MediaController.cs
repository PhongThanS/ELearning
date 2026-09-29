using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Media;
using ELearning.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace ELearning.Api.Controllers;

/// <summary>Ảnh trong nội dung câu hỏi (D-27, docs/05-api.md mục 6.3).</summary>
[Route("api/media")]
public sealed class MediaController(IMediaService media, TimeProvider time) : ApiControllerBase
{
    /// <summary>Giới hạn thân request; kích thước ảnh thật được kiểm tra theo Media:MaxBytes.</summary>
    private const long MaxRequestBytes = 10 * 1024 * 1024 + 64 * 1024;

    /// <summary>Tải ảnh lên (PNG / JPEG / GIF / WebP). Cùng nội dung thì trả lại ảnh đã có.</summary>
    [HttpPost]
    [HasPermission(Permissions.QuestionCreate)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ApiResponse<MediaUploadDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        return ToResponse(await media.UploadAsync(stream, file?.FileName, ct), StatusCodes.Status201Created);
    }

    /// <summary>
    /// Ảnh theo URL đã ký (<c>exp</c>, <c>sig</c> do server sinh trong DTO). Không cần đăng nhập vì thẻ &lt;img&gt; không gửi
    /// được access token; chữ ký là quyền xem. Không rate limit theo IP: cả phòng thi sau một IP NAT cùng tải ảnh.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [DisableRateLimiting]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(Guid id, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken ct)
    {
        var result = await media.OpenAsync(id, exp, sig, ct);
        if (result.IsFailure)
        {
            return Failure(result);
        }

        var file = result.Value;
        var maxAge = Math.Max(0, exp - time.GetUtcNow().ToUnixTimeSeconds());
        Response.Headers.CacheControl = $"private, max-age={maxAge}, immutable";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        // Phòng khi file bị mở trực tiếp: không chạy gì, không nhúng được vào trang khác
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        return File(file.Content, file.ContentType, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{file.Sha256}\""));
    }
}

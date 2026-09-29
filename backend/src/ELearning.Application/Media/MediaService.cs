using System.Security.Cryptography;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Media;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Media;

/// <summary>Lưu file ảnh theo SHA-256 (Infrastructure: thư mục trên đĩa, D-27). File đã có thì không ghi lại.</summary>
public interface IMediaStore
{
    Task SaveAsync(string sha256, ReadOnlyMemory<byte> content, CancellationToken ct);

    /// <summary>Mở file để đọc; null nếu không có trên đĩa.</summary>
    Stream? OpenRead(string sha256);
}

/// <param name="Markdown">Chuỗi chèn vào nội dung: <c>![](media:&lt;id&gt;)</c>.</param>
public sealed record MediaUploadDto(Guid Id, string ContentType, long SizeBytes, string Url, string Markdown);

public sealed record MediaContent(Stream Content, string ContentType, string Sha256);

public interface IMediaService
{
    Task<Result<MediaUploadDto>> UploadAsync(Stream content, string? fileName, CancellationToken ct);

    /// <summary>Mở ảnh theo URL đã ký; sai chữ ký, hết hạn hay không có ảnh đều là NotFound (không lộ lý do).</summary>
    Task<Result<MediaContent>> OpenAsync(Guid id, long expires, string? signature, CancellationToken ct);

    /// <summary>Lỗi MEDIA_NOT_FOUND cho mỗi tham chiếu <c>media:&lt;id&gt;</c> trỏ tới ảnh không tồn tại.</summary>
    Task<IReadOnlyList<Error>> ValidateReferencesAsync(IEnumerable<string?> texts, string field, CancellationToken ct);
}

internal sealed class MediaService(
    IAppDbContext db,
    IMediaStore store,
    MediaLinks links,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IOptions<MediaOptions> options) : IMediaService
{
    private static readonly Error NotFound = Error.NotFound(ErrorCodes.MediaNotFound, "Không tìm thấy ảnh.");

    public async Task<Result<MediaUploadDto>> UploadAsync(Stream content, string? fileName, CancellationToken ct)
    {
        var maxBytes = options.Value.MaxBytes;
        var bytes = await ReadAtMostAsync(content, maxBytes + 1, ct);
        if (bytes.Length == 0)
        {
            return Error.Validation(ErrorCodes.MediaFileRequired, "Vui lòng chọn file ảnh.", "file");
        }

        if (bytes.Length > maxBytes)
        {
            return Error.Validation(ErrorCodes.MediaTooLarge, $"Ảnh tối đa {maxBytes / 1024 / 1024} MB.", "file");
        }

        // Nhận diện theo chữ ký file, không tin phần mở rộng hay Content-Type client gửi.
        var contentType = MediaTypes.Detect(bytes);
        if (contentType is null)
        {
            return Error.Validation(ErrorCodes.MediaTypeNotAllowed, "Chỉ nhận ảnh PNG, JPEG, GIF hoặc WebP.", "file");
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await db.MediaFiles.AsNoTracking().SingleOrDefaultAsync(m => m.Sha256 == sha256, ct);
        if (existing is null)
        {
            await store.SaveAsync(sha256, bytes, ct);
            existing = new MediaFile(sha256, contentType, bytes.Length, fileName, currentUser.RequiredUserId, time.GetUtcNow().UtcDateTime);
            db.MediaFiles.Add(existing);
            audit.Write(AuditActions.MediaUploaded, nameof(MediaFile), existing.Id, newValue: new { existing.ContentType, existing.SizeBytes, existing.OriginalFileName });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Hai người cùng tải một ảnh cùng lúc (trùng UQ_MediaFiles_Sha256): dùng bản đã lưu trước.
                db.ChangeTracker.Clear();
                existing = await db.MediaFiles.AsNoTracking().SingleOrDefaultAsync(m => m.Sha256 == sha256, ct);
                if (existing is null)
                {
                    throw;
                }
            }
        }

        return new MediaUploadDto(
            existing.Id, existing.ContentType, existing.SizeBytes, links.Url(existing.Id), $"![]({MediaLinks.Scheme}{existing.Id:D})");
    }

    public async Task<Result<MediaContent>> OpenAsync(Guid id, long expires, string? signature, CancellationToken ct)
    {
        if (!links.Verify(id, expires, signature))
        {
            return NotFound;
        }

        var media = await db.MediaFiles.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
        var stream = media is null ? null : store.OpenRead(media.Sha256);
        return stream is null ? NotFound : new MediaContent(stream, media!.ContentType, media.Sha256);
    }

    public async Task<IReadOnlyList<Error>> ValidateReferencesAsync(IEnumerable<string?> texts, string field, CancellationToken ct)
    {
        var ids = MediaLinks.ExtractIds(texts);
        if (ids.Count == 0)
        {
            return [];
        }

        var found = await db.MediaFiles.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        return ids.Except(found)
            .Select(id => Error.Validation(ErrorCodes.MediaNotFound, $"Ảnh {MediaLinks.Scheme}{id:D} không tồn tại. Hãy tải ảnh lên lại.", field))
            .ToList();
    }

    private static async Task<byte[]> ReadAtMostAsync(Stream content, int limit, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length < limit && (read = await content.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - buffer.Length)), ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <summary>Nhận diện ảnh theo vài byte đầu (magic number).</summary>
public static class MediaTypes
{
    public static string? Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..] => "image/gif",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };
}

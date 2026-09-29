using ELearning.Domain.Common;

namespace ELearning.Domain.Media;

/// <summary>
/// Ảnh dùng trong nội dung câu hỏi, lựa chọn, giải thích (tham chiếu <c>media:&lt;id&gt;</c>, D-27).
/// Bất biến: không sửa, không xóa (D-16), để snapshot của version đã publish luôn hiển thị đúng ảnh (D-01).
/// File nằm trên đĩa theo SHA-256; cùng nội dung chỉ lưu một lần.
/// </summary>
public sealed class MediaFile : Entity
{
    public const int Sha256Length = 64;
    public const int MaxFileNameLength = 255;

    /// <summary>Loại ảnh được nhận. Không có SVG vì SVG chứa được script (docs/07-bao-mat.md mục 6).</summary>
    public static readonly IReadOnlyList<string> AllowedContentTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    private MediaFile()
    {
    }

    public MediaFile(string sha256, string contentType, long sizeBytes, string? originalFileName, Guid createdBy, DateTime createdAt)
    {
        if (sha256.Length != Sha256Length || !sha256.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("SHA-256 phải là 64 ký tự hex thường.", nameof(sha256));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new ArgumentException($"Không nhận loại file {contentType}.", nameof(contentType));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        Sha256 = sha256;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        OriginalFileName = string.IsNullOrWhiteSpace(originalFileName) ? null : Truncate(Path.GetFileName(originalFileName.Trim()));
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public string Sha256 { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string? OriginalFileName { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Đường dẫn tương đối trong thư mục lưu ảnh: 2 ký tự đầu của hash làm thư mục con.</summary>
    public string StoragePath => StoragePathFor(Sha256);

    public static string StoragePathFor(string sha256) => $"{sha256[..2]}/{sha256}";

    private static string Truncate(string value) => value.Length <= MaxFileNameLength ? value : value[..MaxFileNameLength];
}

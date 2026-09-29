using ELearning.Application.Common.Options;
using ELearning.Application.Media;
using ELearning.Domain.Media;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Media;

/// <summary>
/// Ảnh nằm ở Media:RootPath/&lt;2 ký tự đầu hash&gt;/&lt;sha256&gt; (D-27). Ghi ra file tạm rồi đổi tên, nên không bao giờ
/// có file ghi dở mang tên hash; file đã có thì giữ nguyên (cùng hash là cùng nội dung, không bao giờ ghi đè).
/// </summary>
internal sealed class FileSystemMediaStore(IOptions<MediaOptions> options) : IMediaStore
{
    private string Root => Path.GetFullPath(options.Value.RootPath);

    public async Task SaveAsync(string sha256, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        var path = PathFor(sha256);
        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = Path.Combine(Root, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, content, ct);
            try
            {
                File.Move(temp, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Request khác vừa lưu cùng ảnh
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public Stream? OpenRead(string sha256)
    {
        var path = PathFor(sha256);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;
    }

    private string PathFor(string sha256)
    {
        if (sha256.Length != MediaFile.Sha256Length || !sha256.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("Hash không hợp lệ.", nameof(sha256));
        }

        return Path.Combine(Root, MediaFile.StoragePathFor(sha256));
    }
}

namespace ELearning.Application.Videos;

/// <summary>Thông tin công khai của một video YouTube, dùng để tự điền tiêu đề / ảnh bìa khi thêm bài giảng.</summary>
public sealed record YoutubeVideoInfo(string VideoId, string Title, string? AuthorName, string ThumbnailUrl);

/// <summary>
/// Hỏi YouTube thông tin công khai của video (oEmbed, không cần API key). Trả null khi video riêng tư / đã xoá /
/// không kết nối được YouTube — người dùng tự nhập tiêu đề, không chặn việc lưu bài giảng.
/// </summary>
public interface IYoutubeMetadataClient
{
    Task<YoutubeVideoInfo?> GetAsync(string videoId, CancellationToken ct);
}

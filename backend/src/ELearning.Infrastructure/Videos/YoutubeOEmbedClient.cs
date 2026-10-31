using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ELearning.Application.Videos;
using ELearning.Domain.Videos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ELearning.Infrastructure.Videos;

/// <summary>
/// Lấy tiêu đề video qua https://www.youtube.com/oembed. URL gọi đi chỉ dựng từ mã video 11 ký tự đã kiểm tra
/// (không chuyển tiếp URL người dùng nhập) nên không thể bị lợi dụng để gọi tới máy khác. Kết quả được nhớ 1 giờ.
/// </summary>
internal sealed partial class YoutubeOEmbedClient(HttpClient http, IMemoryCache cache, ILogger<YoutubeOEmbedClient> logger) : IYoutubeMetadataClient
{
    public const string BaseAddress = "https://www.youtube.com/";

    public async Task<YoutubeVideoInfo?> GetAsync(string videoId, CancellationToken ct)
    {
        if (!VideoLesson.IsYoutubeVideoId(videoId))
        {
            return null;
        }

        var key = $"youtube-oembed:{videoId}";
        if (cache.TryGetValue(key, out YoutubeVideoInfo? cached))
        {
            return cached;
        }

        var watchUrl = Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}");
        try
        {
            using var response = await http.GetAsync(new Uri($"oembed?format=json&url={watchUrl}", UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, videoId, (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<OEmbedResponse>(ct);
            if (string.IsNullOrWhiteSpace(body?.Title))
            {
                return null;
            }

            var title = body.Title.Trim();
            var info = new YoutubeVideoInfo(
                videoId,
                title.Length > VideoLesson.MaxTitleLength ? title[..VideoLesson.MaxTitleLength] : title,
                string.IsNullOrWhiteSpace(body.AuthorName) ? null : body.AuthorName.Trim(),
                VideoLesson.YoutubeThumbnail(videoId));
            cache.Set(key, info, TimeSpan.FromHours(1));
            return info;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            LogFailed(logger, videoId, ex.GetType().Name);
            return null;
        }
    }

    private sealed record OEmbedResponse(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author_name")] string? AuthorName);

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube oEmbed không có thông tin cho video {VideoId} (HTTP {Status})")]
    private static partial void LogUnavailable(ILogger logger, string videoId, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không gọi được YouTube oEmbed cho video {VideoId}: {Error}")]
    private static partial void LogFailed(ILogger logger, string videoId, string error);
}

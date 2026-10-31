using ELearning.Application.Videos;

namespace ELearning.ApiTests.Videos;

/// <summary>YouTube giả cho API test: chỉ biết một video công khai, các mã khác coi như riêng tư / đã xoá.</summary>
public sealed class FakeYoutubeMetadataClient : IYoutubeMetadataClient
{
    public const string KnownId = "czKd9vBBtGY";
    public const string KnownTitle = "Người Tiền Sử Ăn Gì Trước Khi Biết Sử Dụng Lửa?";

    public Task<YoutubeVideoInfo?> GetAsync(string videoId, CancellationToken ct) =>
        Task.FromResult(videoId == KnownId
            ? new YoutubeVideoInfo(videoId, KnownTitle, "Xấu Vê Nờ", $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg")
            : null);
}

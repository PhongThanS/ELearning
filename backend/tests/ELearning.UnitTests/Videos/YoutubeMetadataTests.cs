using System.Net;
using System.Text;
using ELearning.Domain.Videos;
using ELearning.Infrastructure.Videos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELearning.UnitTests.Videos;

/// <summary>Tự lấy tiêu đề / ảnh bìa khi gắn link YouTube cho bài giảng.</summary>
public class YoutubeMetadataTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=czKd9vBBtGY", "czKd9vBBtGY")]
    [InlineData("https://youtu.be/czKd9vBBtGY?si=abc", "czKd9vBBtGY")]
    [InlineData("https://www.youtube.com/shorts/czKd9vBBtGY", "czKd9vBBtGY")]
    [InlineData("https://www.youtube.com/watch?list=PL1&v=czKd9vBBtGY", "czKd9vBBtGY")]
    [InlineData("  czKd9vBBtGY ", "czKd9vBBtGY")]
    [InlineData("https://drive.google.com/file/d/16QH9phhlbP6krMALvnC8UUU-uJ-OLqYX/view", null)]
    [InlineData("", null)]
    public void Extracts_video_id_from_youtube_links(string url, string? expected) =>
        VideoLesson.TryExtractYoutubeId(url).Should().Be(expected);

    [Fact]
    public async Task OEmbed_title_and_author_are_returned_and_cached()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"title":"  Người Tiền Sử Ăn Gì  ","author_name":"Xấu Vê Nờ","thumbnail_url":"https://i.ytimg.com/x.jpg"}""");
        var client = Client(handler);

        var first = await client.GetAsync("czKd9vBBtGY", CancellationToken.None);
        var second = await client.GetAsync("czKd9vBBtGY", CancellationToken.None);

        first.Should().NotBeNull();
        first!.Title.Should().Be("Người Tiền Sử Ăn Gì");
        first.AuthorName.Should().Be("Xấu Vê Nờ");
        first.ThumbnailUrl.Should().Be("https://img.youtube.com/vi/czKd9vBBtGY/hqdefault.jpg");
        second.Should().Be(first);
        handler.Requests.Should().ContainSingle("lần hai lấy từ bộ nhớ đệm").Which.Should().Be(
            "https://www.youtube.com/oembed?format=json&url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3DczKd9vBBtGY");
    }

    [Fact]
    public async Task Private_or_missing_video_returns_null_and_bad_ids_never_call_out()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, "Unauthorized");
        var client = Client(handler);

        (await client.GetAsync("czKd9vBBtGY", CancellationToken.None)).Should().BeNull();
        (await client.GetAsync("../../evil", CancellationToken.None)).Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    private static YoutubeOEmbedClient Client(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(YoutubeOEmbedClient.BaseAddress) },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<YoutubeOEmbedClient>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

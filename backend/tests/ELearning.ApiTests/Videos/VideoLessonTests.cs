using System.Net;

namespace ELearning.ApiTests.Videos;

public sealed record VideoView(Guid Id, string Title, string VideoUrl, string? DocumentUrl, string? YoutubeVideoId, string RowVersion);

public sealed record YoutubeInfoView(string VideoId, string Title, string? AuthorName, string ThumbnailUrl);

/// <summary>Bài giảng gồm video YouTube và tài liệu PDF trên Google Drive, xem song song (docs/05-api.md).</summary>
[Collection(ApiCollection.Name)]
public class VideoLessonTests(ApiFactory factory)
{
    private const string Video = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
    private const string Pdf = "https://drive.google.com/file/d/16QH9phhlbP6krMALvnC8UUU-uJ-OLqYX/view?usp=sharing";

    [Fact]
    public async Task Lesson_keeps_video_and_drive_document_and_document_can_be_removed()
    {
        var admin = await AdminAsync();

        var (created, createdBody) = await admin.PostJsonAsync<VideoView>("/api/videos", new { title = $"Bài {Guid.NewGuid():N}", videoUrl = Video, documentUrl = $"  {Pdf} " });
        var lesson = createdBody.Data!;
        var (_, updated) = await admin.SendJsonAsync<VideoView>(HttpMethod.Put, $"/api/videos/{lesson.Id}", new
        {
            title = lesson.Title,
            videoUrl = Video,
            documentUrl = "",
            displayOrder = 0,
            isActive = true,
            rowVersion = lesson.RowVersion,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        lesson.DocumentUrl.Should().Be(Pdf);
        lesson.YoutubeVideoId.Should().Be("dQw4w9WgXcQ");
        updated.Data!.DocumentUrl.Should().BeNull("bỏ trống là bỏ tài liệu đi kèm");
    }

    [Theory]
    [InlineData("https://example.com/bai-giang.pdf")]
    [InlineData("http://drive.google.com/file/d/abc/view")]
    [InlineData("không phải link")]
    public async Task Document_must_be_a_google_drive_link(string documentUrl)
    {
        var admin = await AdminAsync();

        var (response, body) = await admin.PostJsonAsync<VideoView>("/api/videos", new { title = "Bài có tài liệu lạ", videoUrl = Video, documentUrl });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Errors.Should().Contain(e => e.Code == "INVALID_DOCUMENT_URL" && e.Field == "documentUrl");
    }

    [Fact]
    public async Task Youtube_link_returns_title_and_thumbnail_for_the_form()
    {
        var admin = await AdminAsync();
        var anonymous = factory.CreateHttpsClient();
        static string Url(string link) => $"/api/videos/youtube-info?url={Uri.EscapeDataString(link)}";

        var (ok, okBody) = await admin.GetJsonAsync<YoutubeInfoView>(Url($"https://youtu.be/{FakeYoutubeMetadataClient.KnownId}?si=x"));
        var (unavailable, unavailableBody) = await admin.GetJsonAsync<YoutubeInfoView>(Url("https://www.youtube.com/watch?v=AAAAAAAAAAA"));
        var (notYoutube, notYoutubeBody) = await admin.GetJsonAsync<YoutubeInfoView>(Url("https://example.com/video"));
        var noLogin = await anonymous.GetAsync(new Uri(Url(Video), UriKind.Relative));

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        okBody.Data!.Title.Should().Be(FakeYoutubeMetadataClient.KnownTitle);
        okBody.Data.ThumbnailUrl.Should().Be($"https://img.youtube.com/vi/{FakeYoutubeMetadataClient.KnownId}/hqdefault.jpg");
        unavailable.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "video riêng tư / đã xoá: người dùng tự nhập tiêu đề");
        unavailableBody.Errors.Should().Contain(e => e.Code == "YOUTUBE_INFO_UNAVAILABLE");
        notYoutube.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notYoutubeBody.Errors.Should().Contain(e => e.Code == "INVALID_YOUTUBE_URL");
        noLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }
}

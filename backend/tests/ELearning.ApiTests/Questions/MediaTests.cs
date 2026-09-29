using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Exams;

namespace ELearning.ApiTests.Questions;

public sealed record MediaUpload(Guid Id, string ContentType, long SizeBytes, string Url, string Markdown);

/// <summary>Ảnh trong câu hỏi (D-27): upload, URL đã ký, chỉ ký ảnh của nội dung được xem (docs/07-bao-mat.md mục 6).</summary>
[Collection(ApiCollection.Name)]
public class MediaTests(ApiFactory factory)
{
    /// <summary>PNG 1×1; thêm byte sau IEND để mỗi test có một ảnh khác nhau (cùng hash thì dùng chung ảnh).</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Uploaded_image_is_served_by_signed_url_and_tampering_is_404()
    {
        var admin = await AdminAsync();
        var bytes = UniquePng();

        var (created, upload) = await UploadAsync(admin, bytes, "hinh.png");
        var (_, again) = await UploadAsync(admin, bytes, "ban-sao.png");
        var anonymous = factory.CreateHttpsClient();
        var image = await anonymous.GetAsync(new Uri(upload.Data!.Url, UriKind.Relative));
        var tampered = await anonymous.GetAsync(new Uri(upload.Data.Url[..^2] + "xx", UriKind.Relative));
        var unsigned = await anonymous.GetAsync(new Uri($"/api/media/{upload.Data.Id}", UriKind.Relative));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        upload.Data.ContentType.Should().Be("image/png");
        upload.Data.Markdown.Should().Be($"![](media:{upload.Data.Id})");
        again.Data!.Id.Should().Be(upload.Data.Id, "cùng nội dung chỉ lưu một lần");
        image.StatusCode.Should().Be(HttpStatusCode.OK, "thẻ <img> không gửi token; chữ ký là quyền xem");
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        image.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        tampered.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unsigned.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_rejects_non_images_oversize_empty_and_students()
    {
        var admin = await AdminAsync();
        var (student, _, _) = await (await AttemptTestKit.CreateAsync(factory)).CreateStudentAsync();

        var (svg, svgBody) = await UploadAsync(admin, Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"), "x.png");
        var (large, largeBody) = await UploadAsync(admin, [.. Png, .. new byte[2 * 1024 * 1024]], "lon.png");
        var (empty, emptyBody) = await UploadAsync(admin, [], "rong.png");
        var (forbidden, _) = await UploadAsync(student, UniquePng(), "hv.png");

        svg.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        svgBody.Errors.Single().Code.Should().Be("MEDIA_TYPE_NOT_ALLOWED", "nhận diện theo nội dung, không theo đuôi file");
        large.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        largeBody.Errors.Single().Code.Should().Be("MEDIA_TOO_LARGE");
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        emptyBody.Errors.Single().Code.Should().Be("MEDIA_FILE_REQUIRED");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Question_referencing_an_unknown_image_is_rejected()
    {
        var admin = await AdminAsync();

        var (response, body) = await admin.PostJsonAsync<object>("/api/questions", new
        {
            content = $"Hình ![](media:{Guid.NewGuid()})",
            contentFormat = "MARKDOWN",
            questionType = "SINGLE_CHOICE",
            options = new[] { new { content = "A", isCorrect = true }, new { content = "B", isCorrect = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Errors.Single().Code.Should().Be("MEDIA_NOT_FOUND");
    }

    [Fact]
    public async Task Student_gets_signed_urls_only_for_content_they_may_see()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var admin = kit.Admin;
        var content = (await UploadAsync(admin, UniquePng(), "de.png")).Body.Data!.Id;
        var option = (await UploadAsync(admin, UniquePng(), "lua-chon.png")).Body.Data!.Id;
        var explanation = (await UploadAsync(admin, UniquePng(), "giai-thich.png")).Body.Data!.Id;
        var questionId = await CreateQuestionAsync(admin, content, option, explanation);
        var (student, _, _) = await kit.CreateStudentAsync();

        var reviewExam = await PublishAsync(admin, questionId, "AFTER_SUBMIT");
        var start = await student.PostAsync(new Uri($"/api/student/exams/{reviewExam}/start", UriKind.Relative), null);
        var startJson = await start.Content.ReadAsStringAsync();
        var attemptId = JsonDocument.Parse(startJson).RootElement.GetProperty("data").GetProperty("attemptId").GetGuid();
        var playerMedia = Media(startJson);
        var imageFromPlayer = await factory.CreateHttpsClient().GetAsync(new Uri(playerMedia[content.ToString()], UriKind.Relative));

        playerMedia.Keys.Should().BeEquivalentTo(content.ToString(), option.ToString());
        startJson.Should().NotContain(explanation.ToString(), "ảnh giải thích không được lộ khi đang thi");
        imageFromPlayer.StatusCode.Should().Be(HttpStatusCode.OK);

        var submit = await student.PostAsync(new Uri($"/api/student/attempts/{attemptId}/submit", UriKind.Relative), null);
        var reviewMedia = Media(await submit.Content.ReadAsStringAsync());
        reviewMedia.Keys.Should().Contain(explanation.ToString(), "AFTER_SUBMIT: xem lại được giải thích sau khi nộp");

        var hiddenExam = await PublishAsync(admin, questionId, "NEVER");
        var (other, _, _) = await kit.CreateStudentAsync();
        var hiddenAttempt = await AttemptTestKit.StartAsync(other, hiddenExam);
        var hiddenSubmit = await other.PostAsync(new Uri($"/api/student/attempts/{hiddenAttempt.AttemptId}/submit", UriKind.Relative), null);
        var hiddenJson = await hiddenSubmit.Content.ReadAsStringAsync();

        hiddenSubmit.StatusCode.Should().Be(HttpStatusCode.OK);
        hiddenJson.Should().NotContain(explanation.ToString(), "ReviewPolicy NEVER: không bao giờ ký ảnh giải thích");
        Media(hiddenJson).Should().BeEmpty();
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }

    private static byte[] UniquePng() => [.. Png, .. Guid.NewGuid().ToByteArray()];

    private static async Task<(HttpResponseMessage Response, Envelope<MediaUpload> Body)> UploadAsync(HttpClient client, byte[] bytes, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);
        var response = await client.PostAsync(new Uri("/api/media", UriKind.Relative), form);
        var text = await response.Content.ReadAsStringAsync();
        return (response, JsonSerializer.Deserialize<Envelope<MediaUpload>>(text, ApiClientExtensions.Json)!);
    }

    private static async Task<Guid> CreateQuestionAsync(HttpClient admin, Guid content, Guid option, Guid explanation)
    {
        var (response, created) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = $"Hình nào đúng? ![](media:{content})",
            contentFormat = "MARKDOWN",
            questionType = "SINGLE_CHOICE",
            options = new[]
            {
                new { content = $"Hình ![](media:{option})", isCorrect = true },
                new { content = "Không hình", isCorrect = false },
            },
            explanation = $"Vì ![](media:{explanation})",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", created.Errors.Select(e => e.Code)));
        return created.Data!.Id;
    }

    private static async Task<Guid> PublishAsync(HttpClient admin, Guid questionId, string reviewPolicy)
    {
        var (_, exam) = await admin.PostJsonAsync<ExamDetail>("/api/exams", new
        {
            code = ApiClientExtensions.UniqueName("MEDIA-"),
            name = "Đề có hình",
            maxAttempts = 1,
            accessMode = "PUBLIC",
            durationMinutes = 30,
            reviewPolicy,
            scoreVisibility = "IMMEDIATE",
        });
        var versionUrl = $"/api/exams/{exam.Data!.Id}/versions/{exam.Data.DraftVersionId}";
        await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { questionId } });
        var (publish, body) = await admin.PostJsonAsync<object>($"{versionUrl}/publish");
        publish.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", body.Errors.Select(e => e.Code)));
        return exam.Data.Id;
    }

    private static Dictionary<string, string> Media(string json)
    {
        var data = JsonDocument.Parse(json).RootElement.GetProperty("data");
        return data.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Object
            ? media.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
            : [];
    }
}

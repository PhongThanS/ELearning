using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Classes;

namespace ELearning.ApiTests.Videos;

public sealed record VideoImportRowView(
    int RowNumber, string? Title, string? VideoUrl, string? CategoryName, List<string> Classes, string? PracticeExamName,
    List<UserImportIssueView> Issues);

public sealed record VideoImportView(bool DryRun, bool Imported, int TotalRows, int ValidRows, int ImportedCount, List<VideoImportRowView> Rows);

public sealed record ImportedVideoView(
    Guid Id, string Title, string VideoUrl, string? DocumentUrl, string? HomeworkUrl, Guid? PracticeExamId, List<RefItem> Classrooms);

/// <summary>Thêm video bài giảng từ Excel (D-44): tất cả hoặc không.</summary>
[Collection(ApiCollection.Name)]
public class VideoImportTests(ApiFactory factory)
{
    private static readonly string[] Headers =
        ["Link video", "Tiêu đề", "Link tài liệu PDF", "Link bài tập về nhà", "Mã chuyên đề", "Lớp", "Mã đề luyện"];

    private const string Pdf = "https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUv/view";

    [Fact]
    public async Task Template_has_the_columns_and_lookup_sheets()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);

        var response = await kit.Admin.GetAsync(new Uri("/api/videos/import/template", UriKind.Relative));
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sheet = workbook.Worksheet("Video");
        Enumerable.Range(1, Headers.Length).Select(c => sheet.Cell(1, c).GetString()).Should().Equal(Headers);
        sheet.LastRowUsed()!.RowNumber().Should().Be(1);
        workbook.Worksheets.Select(s => s.Name).Should().Contain(["HuongDan", "Lop", "ChuyenDe", "DeLuyen"]);
    }

    [Fact]
    public async Task Valid_rows_are_created_with_classes_homework_and_practice_exam()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, classroom) = await kit.Admin.PostJsonAsync<ClassItem>(
            "/api/classes", new { code = ApiClientExtensions.UniqueName("VI-"), name = "Lớp video import" });
        var exam = await kit.CreatePublishedExamAsync(kind: "PRACTICE");
        var title = ApiClientExtensions.UniqueName("Bài import ");
        var file = Xlsx(
            ["https://www.youtube.com/watch?v=dQw4w9WgXcQ", title, Pdf, Pdf, "", classroom.Data!.Code.ToLowerInvariant(), exam.Code],
            ["https://drive.google.com/file/d/1ZyXwVuTsRqPoNmLkJiHgFe/view", title + " 2", "", "", "", "", ""]);

        var (check, checkBody) = await ImportAsync(kit.Admin, file, dryRun: true);
        var (import, importBody) = await ImportAsync(kit.Admin, file, dryRun: false);
        var (_, list) = await kit.Admin.GetJsonAsync<Paged<ImportedVideoView>>($"/api/videos?keyword={Uri.EscapeDataString(title)}&pageSize=100");

        check.StatusCode.Should().Be(HttpStatusCode.OK);
        checkBody.Data!.Should().Match<VideoImportView>(r => r.DryRun && !r.Imported && r.ValidRows == 2);
        import.StatusCode.Should().Be(HttpStatusCode.OK);
        importBody.Data!.Imported.Should().BeTrue(string.Join(" | ", importBody.Data.Rows.SelectMany(r => r.Issues).Select(i => i.Field + ": " + i.Message)));
        importBody.Data.ImportedCount.Should().Be(2);
        var first = list.Data!.Items.Single(v => v.Title == title);
        first.HomeworkUrl.Should().Be(Pdf);
        first.DocumentUrl.Should().Be(Pdf);
        first.PracticeExamId.Should().Be(exam.Id);
        first.Classrooms.Should().ContainSingle(c => c.Id == classroom.Data.Id);
        list.Data.Items.Should().Contain(v => v.Title == title + " 2");
    }

    [Fact]
    public async Task Any_invalid_row_blocks_the_whole_file()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var graded = await kit.CreatePublishedExamAsync();
        var title = ApiClientExtensions.UniqueName("Không tạo ");
        var file = Xlsx(
            ["https://www.youtube.com/watch?v=dQw4w9WgXcQ", title, "", "", "", "", ""],
            ["", "Lỗi", "https://example.com/a.pdf", "https://example.com/b.pdf", "KHONG-CO", "KHONG-CO-LOP", graded.Code]);

        var (import, importBody) = await ImportAsync(kit.Admin, file, dryRun: false);
        var (_, list) = await kit.Admin.GetJsonAsync<Paged<ImportedVideoView>>($"/api/videos?keyword={Uri.EscapeDataString(title)}");

        import.StatusCode.Should().Be(HttpStatusCode.OK);
        importBody.Data!.Should().Match<VideoImportView>(r => !r.Imported && r.ValidRows == 1 && r.ImportedCount == 0);
        importBody.Data.Rows[1].Issues.Select(i => i.Field).Should().Contain(
            ["Link video", "Link tài liệu PDF", "Link bài tập về nhà", "Mã chuyên đề", "Lớp", "Mã đề luyện"]);
        list.Data!.Items.Should().BeEmpty("có dòng lỗi thì không video nào được tạo");
    }

    [Fact]
    public async Task File_with_only_youtube_links_takes_titles_from_youtube()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var file = Build(
            ["Link video"],
            [[$"https://www.youtube.com/watch?v={FakeYoutubeMetadataClient.KnownId}"], ["https://youtu.be/xxxxxxxxxxx"]]);
        var ok = Build(["Link video"], [[$"https://youtu.be/{FakeYoutubeMetadataClient.KnownId}"]]);

        var (_, checkBody) = await ImportAsync(kit.Admin, file, dryRun: true);
        var (import, importBody) = await ImportAsync(kit.Admin, ok, dryRun: false);

        checkBody.Data!.Rows[0].Title.Should().Be(FakeYoutubeMetadataClient.KnownTitle, "chỉ cần link: tiêu đề lấy từ YouTube");
        checkBody.Data.Rows[0].Issues.Should().BeEmpty();
        checkBody.Data.Rows[1].Issues.Should().ContainSingle(i => i.Code == "TITLE_REQUIRED", "video không lấy được tiêu đề thì báo lỗi");
        import.StatusCode.Should().Be(HttpStatusCode.OK);
        importBody.Data!.Imported.Should().BeTrue();
        importBody.Data.Rows[0].Title.Should().Be(FakeYoutubeMetadataClient.KnownTitle);
    }

    private static byte[] Xlsx(params string[][] rows) => Build(Headers, rows);

    private static byte[] Build(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Video");
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static async Task<(HttpResponseMessage Response, Envelope<VideoImportView> Body)> ImportAsync(HttpClient client, byte[] bytes, bool dryRun)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "video.xlsx");
        var response = await client.PostAsync(new Uri($"/api/videos/import?dryRun={dryRun.ToString().ToLowerInvariant()}", UriKind.Relative), form);
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<Envelope<VideoImportView>>(text, ApiClientExtensions.Json)!;
        return (response, body);
    }
}

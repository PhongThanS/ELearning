using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ELearning.ApiTests.Questions;

public sealed record DocxImageView(string Name, string ContentType, string DataBase64, int Part, int? Question);

public sealed record DocxSkippedView(string? Name, string Reason);

public sealed record DocxImagesView(List<DocxImageView> Images, List<DocxSkippedView> Skipped);

/// <summary>Lấy hình từ file đề Word gốc để ghép với file .md (docs/05-api.md, D-29).</summary>
[Collection(ApiCollection.Name)]
public class DocxImagesTests(ApiFactory factory)
{
    private const string Url = "/api/questions/import-markdown/docx-images";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9];

    [Fact]
    public async Task Word_file_returns_images_named_by_part_and_question_and_other_files_are_rejected()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);

        var (ok, okBody) = await PostAsync<DocxImagesView>(admin, Docx(), "de-thi.docx");
        var (notZip, notZipBody) = await PostAsync<object>(admin, Encoding.UTF8.GetBytes("không phải docx"), "de-thi.docx");
        var (wrongType, _) = await PostAsync<object>(admin, Docx(), "de-thi.pdf");
        var (anonymous, _) = await PostAsync<object>(factory.CreateHttpsClient(), Docx(), "de-thi.docx");

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        okBody.Data!.Images.Should().ContainSingle();
        okBody.Data.Images[0].Name.Should().Be("hinh-p2-c3.png");
        Convert.FromBase64String(okBody.Data.Images[0].DataBase64).Should().Equal(Png);
        notZip.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notZipBody.Errors.Should().Contain(e => e.Code == "IMPORT_FILE_INVALID");
        wrongType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<(HttpResponseMessage Response, Envelope<T> Body)> PostAsync<T>(HttpClient client, byte[] bytes, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        var response = await client.PostAsync(new Uri(Url, UriKind.Relative), form);
        var text = await response.Content.ReadAsStringAsync();
        var body = string.IsNullOrEmpty(text)
            ? new Envelope<T>(response.IsSuccessStatusCode, default, null, [], null)
            : JsonSerializer.Deserialize<Envelope<T>>(text, ApiClientExtensions.Json)!;
        return (response, body);
    }

    private static byte[] Docx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "word/document.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><w:body>
                  <w:p><w:r><w:t>PHẦN II. ĐÚNG SAI</w:t></w:r></w:p>
                  <w:p><w:r><w:t>Câu 3. Cho hình vẽ</w:t></w:r></w:p>
                  <w:p><w:r><w:drawing><a:blip r:embed="rId1"/></w:drawing></w:r></w:p>
                </w:body></w:document>
                """);
            Write(zip, "word/_rels/document.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
                </Relationships>
                """);
            using var image = zip.CreateEntry("word/media/image1.png").Open();
            image.Write(Png);
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

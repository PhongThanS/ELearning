using System.IO.Compression;
using System.Text;
using ELearning.Application.Questions;

namespace ELearning.UnitTests.Questions;

/// <summary>Lấy ảnh từ file đề Word gốc, đặt tên theo phần / câu như prompt (docs/02-nghiep-vu.md mục 1.5, D-29).</summary>
public class DocxImageExtractorTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 4, 5, 6];
    private static readonly byte[] Wmf = [0xD7, 0xCD, 0xC6, 0x9A, 0, 0];

    [Fact]
    public void Images_are_named_by_part_and_question_and_formulas_are_skipped()
    {
        var docx = Docx(
            [
                P("PHẦN I. CÂU TRẮC NGHIỆM NHIỀU PHƯƠNG ÁN LỰA CHỌN"),
                P("Câu 1. Cho hàm số") + Formula("rIdEq"),
                P("Câu 3. Cho hàm số có đồ thị như hình vẽ."),
                Picture("rIdA"),
                P("A. 3.B. 2."),
                P("Câu 4: Hai hình") + Picture("rIdB") + Picture("rIdC"),
                P("Phần II. Đúng sai"),
                Picture("rIdShared"),
                P("Câu 1. Cho hình hộp") + Picture("rIdWmf"),
                P("BẢNG ĐÁP ÁN"),
                P("Câu 3. Lời giải") + Picture("rIdSolution"),
            ],
            new()
            {
                ["rIdEq"] = ("media/image1.wmf", Wmf),
                ["rIdA"] = ("media/image2.png", Png),
                ["rIdB"] = ("media/image3.jpeg", Jpeg),
                ["rIdC"] = ("media/image4.png", Png),
                ["rIdShared"] = ("media/image5.png", Png),
                ["rIdWmf"] = ("media/image6.wmf", Wmf),
                ["rIdSolution"] = ("media/image7.png", Png),
            });

        var result = DocxImageExtractor.Extract(docx, 1024)!;

        result.Images.Select(i => i.Name).Should().Equal(
            "hinh-p1-c3.png", "hinh-p1-c4.jpg", "hinh-p1-c4-2.png", "hinh-p2-chung.png");
        result.Images[1].ContentType.Should().Be("image/jpeg");
        Convert.FromBase64String(result.Images[0].DataBase64).Should().Equal(Png);
        result.Skipped.Should().ContainSingle(s => s.Name == "hinh-p2-c1" && s.Reason.Contains("WMF"));
    }

    [Fact]
    public void Same_picture_used_again_in_the_solutions_is_taken_once()
    {
        var docx = Docx(
            [P("Câu 2. Đồ thị") + Picture("rIdA"), P("Lời giải câu 2:") + Picture("rIdA"), P("Câu 5. Hình") + Picture("rIdB")],
            new() { ["rIdA"] = ("media/a.png", Png), ["rIdB"] = ("media/b.png", Png) });

        DocxImageExtractor.Extract(docx, 1024)!.Images.Select(i => i.Name).Should().Equal("hinh-p1-c2.png", "hinh-p1-c5.png");
    }

    [Fact]
    public void Answer_written_under_a_question_does_not_stop_extraction_and_big_images_are_skipped()
    {
        var docx = Docx(
            [P("Câu 1. Hình") + Picture("rIdA"), P("ĐÁP ÁN: B"), P("Câu 2. Hình to") + Picture("rIdBig")],
            new() { ["rIdA"] = ("media/a.png", Png), ["rIdBig"] = ("media/big.png", [.. Png, .. new byte[2048]]) });

        var result = DocxImageExtractor.Extract(docx, 1024)!;

        result.Images.Select(i => i.Name).Should().Equal("hinh-p1-c1.png");
        result.Skipped.Should().ContainSingle(s => s.Name == "hinh-p1-c2");
    }

    [Fact]
    public void Not_a_docx_returns_null()
    {
        DocxImageExtractor.Extract(new MemoryStream(Encoding.UTF8.GetBytes("không phải zip")), 1024).Should().BeNull();
    }

    private static string P(string text) => $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";

    private static string Picture(string relId) =>
        $"<w:p><w:r><w:drawing><wp:inline><a:graphic><a:graphicData><pic:pic><pic:blipFill><a:blip r:embed=\"{relId}\"/></pic:blipFill></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>";

    /// <summary>Công thức MathType: đối tượng OLE kèm ảnh xem trước VML — không phải hình vẽ.</summary>
    private static string Formula(string relId) =>
        $"<w:p><w:r><w:object><v:shape><v:imagedata r:id=\"{relId}\"/></v:shape></w:object></w:r></w:p>";

    private static MemoryStream Docx(string[] paragraphs, Dictionary<string, (string Target, byte[] Bytes)> media)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                  xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                  xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"
                  xmlns:v="urn:schemas-microsoft-com:vml"><w:body>{string.Concat(paragraphs)}</w:body></w:document>
                """);
            var rels = string.Concat(media.Select(m =>
                $"<Relationship Id=\"{m.Key}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"{m.Value.Target}\"/>"));
            Write(zip, "word/_rels/document.xml.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{rels}</Relationships>");
            foreach (var (target, bytes) in media.Values)
            {
                using var entry = zip.CreateEntry("word/" + target).Open();
                entry.Write(bytes);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ELearning.Application.Media;

namespace ELearning.Application.Questions;

/// <summary>Một ảnh lấy từ file Word, đã đặt tên theo quy ước của prompt (<c>hinh-p1-c3.png</c>).</summary>
public sealed record DocxImage(string Name, string ContentType, string DataBase64, int Part, int? Question, int Bytes);

/// <summary>Ảnh có trong đề nhưng không lấy được (định dạng WMF/EMF, quá lớn…), để người dùng cắt tay.</summary>
public sealed record DocxSkippedImage(string? Name, int Part, int? Question, string Reason);

public sealed record DocxImageResult(IReadOnlyList<DocxImage> Images, IReadOnlyList<DocxSkippedImage> Skipped);

/// <summary>
/// Lấy ảnh (hình vẽ, đồ thị) từ file đề .docx gốc và đặt tên theo vị trí trong đề, đúng quy ước mà prompt yêu cầu AI
/// ghi trong file .md: <c>hinh-p&lt;phần&gt;-c&lt;câu&gt;[-2].png</c>, ảnh trước câu đầu tiên của phần là
/// <c>hinh-p&lt;phần&gt;-chung.png</c> (docs/02-nghiep-vu.md mục 1.5, D-29). Không dùng AI: phần nhận từ dòng
/// "PHẦN I/II/…", câu nhận từ dòng "Câu 3." / "Bài 3:". Dừng ở bảng đáp án / lời giải để ảnh trong lời giải
/// không bị đặt nhầm tên. Công thức MathType (đối tượng OLE) không phải ảnh nên bị bỏ qua.
/// </summary>
public static partial class DocxImageExtractor
{
    public const int MaxImages = 200;

    private const long MaxXmlBytes = 64L * 1024 * 1024;

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Trả null khi không phải file Word hợp lệ.</summary>
    public static DocxImageResult? Extract(Stream docx, int maxImageBytes)
    {
        try
        {
            using var zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
            var document = zip.GetEntry("word/document.xml");
            if (document is null || document.Length > MaxXmlBytes)
            {
                return null;
            }

            var xml = Load(document);
            var targets = LoadRelationships(zip.GetEntry("word/_rels/document.xml.rels"));
            return Collect(zip, xml, targets, maxImageBytes);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException)
        {
            return null;
        }
    }

    private static DocxImageResult Collect(ZipArchive zip, XDocument xml, Dictionary<string, string> targets, int maxImageBytes)
    {
        var images = new List<DocxImage>();
        var skipped = new List<DocxSkippedImage>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var part = 1;
        var partSeen = false;
        int? question = null;

        foreach (var p in xml.Descendants(W + "p"))
        {
            var text = ParagraphText(p);
            if (StopHeading().IsMatch(text))
            {
                break;
            }

            var partMatch = PartHeading().Match(text);
            if (partMatch.Success)
            {
                // "PHẦN I" của đề đầu tiên giữ số 1; các phần sau đánh số theo đề (II → 2…)
                part = PartNumber(partMatch.Groups["n"].Value) ?? (partSeen ? part + 1 : 1);
                partSeen = true;
                question = null;
            }
            else if (QuestionStart().Match(text) is { Success: true } q)
            {
                question = int.Parse(q.Groups["n"].Value, CultureInfo.InvariantCulture);
            }

            foreach (var relId in PictureRelIds(p))
            {
                if (!targets.TryGetValue(relId, out var target) || !seenTargets.Add(target))
                {
                    continue;
                }

                var stem = question is { } n ? $"hinh-p{part}-c{n}" : $"hinh-p{part}-chung";
                var index = counts[stem] = counts.GetValueOrDefault(stem) + 1;
                var baseName = index == 1 ? stem : $"{stem}-{index}";
                if (images.Count >= MaxImages)
                {
                    skipped.Add(new(null, part, question, $"Đề có hơn {MaxImages} ảnh, chỉ lấy {MaxImages} ảnh đầu."));
                    return new DocxImageResult(images, skipped);
                }

                var entry = zip.GetEntry(target);
                if (entry is null)
                {
                    continue;
                }

                if (entry.Length > maxImageBytes)
                {
                    skipped.Add(new(baseName, part, question, $"Ảnh lớn hơn {maxImageBytes / 1024 / 1024} MB, hãy cắt / nén ảnh rồi chọn tay."));
                    continue;
                }

                var bytes = Read(entry, maxImageBytes);
                var contentType = MediaTypes.Detect(bytes);
                if (contentType is null)
                {
                    var ext = Path.GetExtension(target).TrimStart('.').ToUpperInvariant();
                    skipped.Add(new(baseName, part, question,
                        $"Ảnh dạng {(ext.Length > 0 ? ext : "lạ")} trình duyệt không hiển thị được; chụp lại hình thành PNG rồi chọn tay."));
                    continue;
                }

                var extension = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".png",
                };
                images.Add(new DocxImage(baseName + extension, contentType, Convert.ToBase64String(bytes), part, question, bytes.Length));
            }
        }

        return new DocxImageResult(images, skipped);
    }

    /// <summary>Chữ của chính đoạn (không gồm đoạn lồng trong hộp văn bản), chuẩn hóa NFC để so tiếng Việt.</summary>
    private static string ParagraphText(XElement p)
    {
        var sb = new StringBuilder();
        foreach (var t in p.Descendants(W + "t"))
        {
            if (t.Ancestors(W + "p").First() == p)
            {
                sb.Append(t.Value);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    /// <summary>Ảnh của chính đoạn: DrawingML (a:blip) và VML cũ (v:imagedata), trừ ảnh xem trước của công thức (w:object).</summary>
    private static IEnumerable<string> PictureRelIds(XElement p)
    {
        foreach (var el in p.Descendants())
        {
            string? id = null;
            if (el.Name == A + "blip")
            {
                id = (string?)el.Attribute(R + "embed") ?? (string?)el.Attribute(R + "link");
            }
            else if (el.Name == V + "imagedata")
            {
                id = (string?)el.Attribute(R + "id");
            }

            if (id is not null && el.Ancestors(W + "p").First() == p && !el.Ancestors(W + "object").Any())
            {
                yield return id;
            }
        }
    }

    private static Dictionary<string, string> LoadRelationships(ZipArchiveEntry? entry)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entry is null || entry.Length > MaxXmlBytes)
        {
            return map;
        }

        foreach (var r in Load(entry).Descendants(Rel + "Relationship"))
        {
            var id = (string?)r.Attribute("Id");
            var target = (string?)r.Attribute("Target");
            if (id is null || target is null || (string?)r.Attribute("TargetMode") == "External")
            {
                continue;
            }

            // Đường dẫn tương đối so với thư mục word/
            map[id] = target.StartsWith('/') ? target.TrimStart('/') : "word/" + target.Replace("\\", "/", StringComparison.Ordinal);
        }

        return map;
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static byte[] Read(ZipArchiveEntry entry, int limit)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length <= limit && (read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static int? PartNumber(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }

        var roman = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10 };
        var total = 0;
        var upper = value.ToUpperInvariant();
        for (var i = 0; i < upper.Length; i++)
        {
            var current = roman[upper[i]];
            total += i + 1 < upper.Length && roman[upper[i + 1]] > current ? -current : current;
        }

        return total is > 0 and <= 20 ? total : null;
    }

    [GeneratedRegex(@"^[\s\*\-#]*PH[ẦẨA]N\s+(?<n>[IVX]+|\d+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PartHeading();

    [GeneratedRegex(@"^[\s\*\-#]*(?:CÂU|BÀI|QUESTION)\s*(?<n>\d{1,3})(?!\d)\s*(?:[\.:\)\(]|$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex QuestionStart();

    /// <summary>Tiêu đề viết hoa của phần đáp án / lời giải; không bắt "Đáp án: B" / "ĐÁP ÁN: B" ngay dưới một câu.</summary>
    [GeneratedRegex(@"^[\s\*\-#]*(?:BẢNG\s+ĐÁP\s+ÁN|ĐÁP\s+ÁN|LỜI\s+GIẢI|HƯỚNG\s+DẪN\s+(?:GIẢI|CHẤM))(?:\s+(?:VÀ|CHI|TIẾT|THAM|KHẢO|LỜI|GIẢI|HƯỚNG|DẪN|CHẤM|ĐỀ|THI))*[\s\.:\*\-–]*$", RegexOptions.CultureInvariant)]
    private static partial Regex StopHeading();
}

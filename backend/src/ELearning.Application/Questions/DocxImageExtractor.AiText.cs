using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ELearning.Application.Questions;

/// <summary>Đề Word đã chuyển thành văn bản cho AI đọc (docs/02-nghiep-vu.md mục 1.5).</summary>
/// <param name="Text">Nội dung file .md gửi cho AI.</param>
/// <param name="Formulas">Số công thức (MathType + công thức Word) đã gặp.</param>
/// <param name="UnreadFormulas">Số công thức không đổi được, ghi <c>[?]</c> trong văn bản.</param>
/// <param name="Pictures">Số dòng hình đã đặt tên sẵn.</param>
public sealed record DocxAiTextResult(string Text, int Formulas, int UnreadFormulas, int Pictures);

public static partial class DocxImageExtractor
{
    private static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private const int MaxOleBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Chuyển đề Word thành văn bản mà AI nào cũng đọc được: chữ giữ nguyên theo thứ tự, công thức MathType và công thức
    /// Word đổi sang LaTeX trong <c>$…$</c>, số câu / nhãn Word tự đánh được in ra, bảng thành dòng <c>| … |</c>, mỗi hình
    /// thành dòng <c>![hình](hinh-p1-c3.png)</c> đúng tên mà bước nhập tự lấy từ file Word. AI không đọc được MathType
    /// trong .docx (hay tự bịa), nên giáo viên gửi file này thay cho file Word. Trả null khi không phải file Word hợp lệ.
    /// </summary>
    public static DocxAiTextResult? ExtractForAi(Stream docx, string fileName, int maxImageBytes)
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
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Collect(zip, xml, targets, WordNumbering.Load(zip.GetEntry("word/numbering.xml")), maxImageBytes, names);

            // Đếm lại từ đầu để in nhãn Word tự đánh cho mọi đoạn, đúng thứ tự như Word
            var numbering = WordNumbering.Load(zip.GetEntry("word/numbering.xml"));
            var labels = new Dictionary<XElement, string>();
            foreach (var p in xml.Descendants(W + "p"))
            {
                if (numbering.Label(p) is { } label)
                {
                    labels[p] = label;
                }
            }

            var writer = new AiTextWriter(zip, targets, names, labels);
            var body = xml.Root?.Element(W + "body");
            if (body is not null)
            {
                writer.Block(body);
            }

            var header = string.Create(CultureInfo.InvariantCulture,
                $"""
                <!-- SClass: đề chuyển từ file Word "{fileName}" để AI đọc. Công thức đã đổi sang LaTeX trong $…$ (chép nguyên). Dòng ảnh trỏ tới hinh-…png là hình đã đặt tên sẵn: giữ nguyên tên. [?] là công thức không đọc được. -->


                """);
            return new DocxAiTextResult(header + writer.Text, writer.Formulas, writer.Unread, writer.Pictures);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException)
        {
            return null;
        }
    }

    private sealed class AiTextWriter(
        ZipArchive zip, Dictionary<string, string> targets, Dictionary<string, string> names, Dictionary<XElement, string> labels)
    {
        private readonly StringBuilder _out = new();
        private readonly HashSet<string> _pictureTargets = new(StringComparer.OrdinalIgnoreCase);

        public int Formulas { get; private set; }

        public int Unread { get; private set; }

        public int Pictures { get; private set; }

        public string Text => MultiBlankLines().Replace(_out.ToString(), "\n\n").Trim() + "\n";

        /// <summary>Thân tài liệu / ô bảng / nội dung điều khiển: đoạn và bảng theo thứ tự.</summary>
        public void Block(XElement container)
        {
            foreach (var el in container.Elements())
            {
                if (el.Name == W + "p")
                {
                    var line = Paragraph(el);
                    if (line.Length > 0)
                    {
                        _out.Append(line).Append("\n\n");
                    }
                }
                else if (el.Name == W + "tbl")
                {
                    Table(el);
                }
                else if (el.Name == W + "sdt")
                {
                    if (el.Element(W + "sdtContent") is { } content)
                    {
                        Block(content);
                    }
                }
            }
        }

        private void Table(XElement tbl)
        {
            foreach (var tr in tbl.Elements(W + "tr"))
            {
                var cells = tr.Elements(W + "tc")
                    .Select(tc => string.Join(" ", tc.Elements(W + "p").Select(Paragraph).Where(s => s.Length > 0)).Replace("|", "\\vert ", StringComparison.Ordinal))
                    .ToList();
                if (cells.Any(c => c.Length > 0))
                {
                    _out.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
                }
            }

            _out.Append('\n');
        }

        private string Paragraph(XElement p)
        {
            var sb = new StringBuilder();
            if (labels.TryGetValue(p, out var label))
            {
                sb.Append(label).Append(' ');
            }

            _pictureTargets.Clear();
            Inline(p, sb);
            var text = SpaceRun().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ");
            text = SpaceBeforePunctuation().Replace(text, "$$$1").Replace("( $", "($", StringComparison.Ordinal);
            return text.Trim();
        }

        private void Inline(XElement el, StringBuilder sb)
        {
            foreach (var child in el.Elements())
            {
                var name = child.Name;
                if (name == W + "t")
                {
                    sb.Append(child.Value);
                }
                else if (name == W + "tab")
                {
                    sb.Append('\t');
                }
                else if (name == W + "br" || name == W + "cr")
                {
                    sb.Append('\n');
                }
                else if (name == W + "object")
                {
                    Ole(child, sb);
                }
                else if (name == OmmlLatex.M + "oMathPara")
                {
                    foreach (var math in child.Elements(OmmlLatex.M + "oMath"))
                    {
                        sb.Append("\n$$").Append(Omml(math)).Append("$$\n");
                    }
                }
                else if (name == OmmlLatex.M + "oMath")
                {
                    sb.Append(" $").Append(Omml(math: child)).Append("$ ");
                }
                else if (name == W + "drawing" || name == W + "pict")
                {
                    Picture(child, sb);
                }
                else if (name == Mc + "AlternateContent")
                {
                    // Word ghi hai bản của cùng một hình (mới + cũ): chỉ đọc bản đầu
                    if (child.Elements().FirstOrDefault() is { } choice)
                    {
                        Inline(choice, sb);
                    }
                }
                else if (name == W + "p")
                {
                    // Đoạn trong hộp văn bản
                    sb.Append(' ');
                    Inline(child, sb);
                    sb.Append(' ');
                }
                else if (name != W + "delText" && name != W + "instrText" && name != W + "pPr" && name != W + "rPr")
                {
                    Inline(child, sb);
                }
            }
        }

        private string Omml(XElement math)
        {
            Formulas++;
            try
            {
                var latex = OmmlLatex.Convert(math);
                return latex.Length > 0 ? latex : Unknown();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                return Unknown();
            }
        }

        private void Ole(XElement obj, StringBuilder sb)
        {
            var ole = obj.Descendants(O + "OLEObject").FirstOrDefault();
            var progId = (string?)ole?.Attribute("ProgID") ?? string.Empty;
            if (!progId.StartsWith("Equation.", StringComparison.OrdinalIgnoreCase))
            {
                // Đối tượng nhúng khác (Excel, hình vẽ…): coi như hình nếu có ảnh xem trước
                Picture(obj, sb);
                return;
            }

            Formulas++;
            var relId = (string?)ole!.Attribute(R + "id");
            string? latex = null;
            if (relId is not null && targets.TryGetValue(relId, out var target) && zip.GetEntry(target) is { } entry && entry.Length <= MaxOleBytes)
            {
                try
                {
                    latex = MathTypeLatex.FromOleObject(Read(entry, MaxOleBytes));
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException or FormatException)
                {
                    latex = null;
                }
            }

            sb.Append(' ').Append(string.IsNullOrWhiteSpace(latex) ? Unknown() : $"${latex}$").Append(' ');
        }

        private string Unknown()
        {
            Unread++;
            return "[?]";
        }

        private void Picture(XElement el, StringBuilder sb)
        {
            foreach (var id in el.Descendants()
                .Select(d => d.Name == A + "blip" ? (string?)d.Attribute(R + "embed") ?? (string?)d.Attribute(R + "link")
                    : d.Name == V + "imagedata" ? (string?)d.Attribute(R + "id") : null)
                .OfType<string>())
            {
                if (!targets.TryGetValue(id, out var target) || !_pictureTargets.Add(target))
                {
                    continue;
                }

                Pictures++;
                sb.Append(names.TryGetValue(target, out var picture)
                    ? $"\n\n![hình]({picture}.png)\n\n"
                    : "\n\n[HÌNH: phần mềm không tự lấy được hình này — chèn dòng ảnh nếu câu cần hình]\n\n");
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(" {2,}")]
    private static partial System.Text.RegularExpressions.Regex SpaceRun();

    /// <summary>"$x$ ." → "$x$.": dấu cách thêm quanh công thức không được đứng trước dấu câu.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"\$ ([.,;:)?!])")]
    private static partial System.Text.RegularExpressions.Regex SpaceBeforePunctuation();

    [System.Text.RegularExpressions.GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")]
    private static partial System.Text.RegularExpressions.Regex MultiBlankLines();
}

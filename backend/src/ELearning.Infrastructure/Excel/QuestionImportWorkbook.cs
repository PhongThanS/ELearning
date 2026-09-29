using System.Globalization;
using ClosedXML.Excel;
using ELearning.Application.Questions;

namespace ELearning.Infrastructure.Excel;

/// <summary>
/// Đọc file import câu hỏi và tạo file mẫu. Chỉ trích giá trị ô thành chuỗi; việc diễn giải nằm ở
/// Application (QuestionImportMapper) để kiểm thử được mà không cần Excel.
/// </summary>
internal sealed class QuestionImportWorkbook : IQuestionImportWorkbook
{
    public const string DataSheetName = "CauHoi";

    public QuestionImportSheet Read(Stream stream, int maxRows)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("File không đúng định dạng Excel (.xlsx).", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(DataSheetName, StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.First();
            var headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
            {
                return new QuestionImportSheet([], []);
            }

            var columns = new Dictionary<int, string>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = QuestionImportColumns.Normalize(Text(cell) ?? string.Empty);
                if (header.Length > 0)
                {
                    columns.TryAdd(cell.Address.ColumnNumber, header);
                }
            }

            var rows = new List<QuestionImportRawRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
            {
                var cells = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (column, header) in columns)
                {
                    if (Text(sheet.Cell(r, column)) is { } value)
                    {
                        cells[header] = value;
                    }
                }

                if (cells.Count == 0)
                {
                    continue; // dòng trống
                }

                if (rows.Count == maxRows)
                {
                    throw new InvalidDataException($"Mỗi lần import tối đa {maxRows} câu hỏi. Hãy tách file.");
                }

                rows.Add(new QuestionImportRawRow(r, cells));
            }

            return new QuestionImportSheet(rows, columns.Values.ToList());
        }
    }

    public byte[] CreateTemplate(IReadOnlyList<(string Code, string Name)> categories)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(DataSheetName);
        var headers = QuestionImportColumns.TemplateHeaders;
        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);

        var category = categories.Count > 0 ? categories[0].Code : string.Empty;
        string?[][] examples =
        [
            ["", category, "SINGLE_CHOICE", "MARKDOWN", "Kiểu dữ liệu nào phù hợp để lưu tiền tệ trong C#?", "1",
                "float", "double", "decimal", "int", "", "", "C", "", "", "", "", "", "", "decimal có độ chính xác thập phân cao.", "Dễ", "kiểu dữ liệu, c#"],
            ["", category, "MULTIPLE_CHOICE", "MARKDOWN", "Những từ khóa nào là **access modifier**?", "2",
                "public", "static", "private", "void", "", "", "A, C", "", "", "", "", "", "", "", "Trung bình", "oop"],
            ["", category, "TRUE_FALSE", "PLAIN", "string trong C# là kiểu tham chiếu.", "1",
                "", "", "", "", "", "", "Đúng", "", "", "", "", "", "", ""],
            ["", category, "FILL_IN", "PLAIN", "Thủ đô của Việt Nam là gì?", "1",
                "", "", "", "", "", "", "", "TEXT", "Hà Nội | Hanoi", "", "", "Không", "Có", ""],
            ["", category, "FILL_IN", "PLAIN", "7 / 2 bằng bao nhiêu (số thực)?", "1",
                "", "", "", "", "", "", "", "NUMBER", "", "3,5", "0", "", "", ""],
        ];
        for (var r = 0; r < examples.Length; r++)
        {
            for (var c = 0; c < examples[r].Length; c++)
            {
                // Ghi mọi giá trị dạng chữ để Excel không tự đổi "3,5" hay "A, C"
                sheet.Cell(r + 2, c + 1).Value = examples[r][c] ?? string.Empty;
            }
        }

        var typeColumn = headers.ToList().IndexOf(QuestionImportColumns.Type) + 1;
        sheet.Range(2, typeColumn, 1001, typeColumn).CreateDataValidation()
            .List("\"SINGLE_CHOICE,MULTIPLE_CHOICE,TRUE_FALSE,FILL_IN\"", true);
        sheet.Columns().AdjustToContents(1, 6, 8, 60);

        var guide = workbook.Worksheets.Add("HuongDan");
        string[][] lines =
        [
            ["Cột", "Cách ghi"],
            [QuestionImportColumns.Code, "Không bắt buộc. Để trống thì hệ thống tự sinh. Không được trùng mã đã có."],
            [QuestionImportColumns.Category, "Mã danh mục (xem sheet DanhMuc). Để trống nếu không phân loại."],
            [QuestionImportColumns.Type, "SINGLE_CHOICE (Chọn một), MULTIPLE_CHOICE (Chọn nhiều), TRUE_FALSE (Đúng / Sai), FILL_IN (Điền đáp án)."],
            [QuestionImportColumns.Format, "MARKDOWN (mặc định) hoặc PLAIN. Markdown không hỗ trợ HTML và hình ảnh."],
            [QuestionImportColumns.Content, "Bắt buộc. Tối đa 10.000 ký tự."],
            [QuestionImportColumns.Score, "Mặc định 1. Từ 0,25 đến 100, bội số của 0,25."],
            ["Lựa chọn A … J", "Câu chọn một / chọn nhiều: 2–10 lựa chọn, ghi liên tục từ A. Có thể thêm cột Lựa chọn G … J."],
            [QuestionImportColumns.Correct, "Chọn một: một chữ cái (C). Chọn nhiều: các chữ cái cách nhau bởi dấu phẩy (A, C). Đúng / Sai: Đúng hoặc Sai."],
            [QuestionImportColumns.AnswerDataType, "Chỉ cho FILL_IN: TEXT (chữ) hoặc NUMBER (số)."],
            [QuestionImportColumns.AcceptedAnswers, "FILL_IN kiểu TEXT: 1–20 đáp án, cách nhau bởi dấu | hoặc xuống dòng."],
            [QuestionImportColumns.CorrectNumber, "FILL_IN kiểu NUMBER: số, dùng dấu phẩy hoặc dấu chấm thập phân (3,5 hoặc 3.5)."],
            [QuestionImportColumns.Tolerance, "FILL_IN kiểu NUMBER: sai số cho phép, mặc định 0."],
            [QuestionImportColumns.CaseSensitive, "Có / Không (mặc định Không)."],
            [QuestionImportColumns.IgnoreAccent, "Có / Không (mặc định Không). Có: \"ha noi\" được tính đúng với \"Hà Nội\"."],
            [QuestionImportColumns.Explanation, "Không bắt buộc. Hiển thị khi học viên xem lại bài (nếu chính sách cho phép)."],
            [QuestionImportColumns.Difficulty, "Không bắt buộc: Dễ, Trung bình, Khó (EASY / MEDIUM / HARD). Dùng để lập pool ngẫu nhiên."],
            [QuestionImportColumns.Tags, "Không bắt buộc: tối đa 10 tag, cách nhau bởi dấu phẩy. Không phân biệt hoa thường."],
            ["", ""],
            ["Lưu ý", $"Tối đa {QuestionImportColumns.MaxRows} câu mỗi lần. Có dòng lỗi thì không câu nào được tạo; sửa theo báo lỗi rồi import lại."],
        ];
        for (var r = 0; r < lines.Length; r++)
        {
            guide.Cell(r + 1, 1).Value = lines[r][0];
            guide.Cell(r + 1, 2).Value = lines[r][1];
        }

        guide.Row(1).Style.Font.Bold = true;
        guide.Column(1).Width = 24;
        guide.Column(2).Width = 110;

        var categorySheet = workbook.Worksheets.Add("DanhMuc");
        categorySheet.Cell(1, 1).Value = "Mã danh mục";
        categorySheet.Cell(1, 2).Value = "Tên";
        categorySheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < categories.Count; i++)
        {
            categorySheet.Cell(i + 2, 1).Value = categories[i].Code;
            categorySheet.Cell(i + 2, 2).Value = categories[i].Name;
        }

        categorySheet.Columns().AdjustToContents();
        sheet.SetTabActive();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    /// <summary>Giá trị ô dạng chuỗi; ô số ghi theo dấu chấm (không phụ thuộc culture), ô trống → null.</summary>
    private static string? Text(IXLCell cell)
    {
        var value = cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            _ => cell.GetFormattedString(),
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}

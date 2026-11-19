using ClosedXML.Excel;
using ELearning.Application.Questions;
using ELearning.Application.Videos;

namespace ELearning.Infrastructure.Excel;

/// <summary>Đọc file import video bài giảng và tạo file mẫu (kèm hướng dẫn, mã lớp, mã chuyên đề, mã đề luyện) — D-44.</summary>
internal sealed class VideoImportWorkbook : IVideoImportWorkbook
{
    public const string DataSheetName = "Video";

    public QuestionImportSheet Read(Stream stream, int maxRows) => ExcelSheetReader.Read(stream, DataSheetName, maxRows, "video");

    public byte[] CreateTemplate(
        IReadOnlyList<(string Code, string Name)> classes,
        IReadOnlyList<(string Code, string Name)> categories,
        IReadOnlyList<(string Code, string Name)> practiceExams)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(DataSheetName);
        var headers = VideoImportColumns.TemplateHeaders;
        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);
        // Sheet dữ liệu chỉ có tiêu đề: ví dụ nằm ở sheet HuongDan để không bị import nhầm
        sheet.Range(2, 1, VideoImportColumns.MaxRows + 1, headers.Count).Style.NumberFormat.Format = "@";
        for (var c = 1; c <= headers.Count; c++)
        {
            sheet.Column(c).Width = c is 1 or 2 ? 45 : 28;
        }

        sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        sheet.Cell(1, 1).GetComment().AddText("Bắt buộc. Chỉ cần dán link YouTube: tiêu đề tự lấy từ YouTube.");

        var guide = workbook.Worksheets.Add("HuongDan");
        string[][] lines =
        [
            ["Cột", "Cách ghi"],
            [VideoImportColumns.VideoUrl, "Bắt buộc — cột duy nhất cần điền. Link YouTube (hoặc link Google Drive chia sẻ \"Bất kỳ ai có đường liên kết\", link file video trực tiếp)."],
            [VideoImportColumns.Title, "Không cần điền với link YouTube: hệ thống tự lấy tiêu đề video từ YouTube. Ghi vào nếu muốn đặt tên khác (tối đa 250 ký tự); link Drive / file video thì bắt buộc ghi."],
            [VideoImportColumns.DocumentUrl, "Không bắt buộc. Tài liệu PDF xem song song với video: link Google Drive / Google Docs."],
            [VideoImportColumns.HomeworkUrl, "Không bắt buộc. File PDF bài tập về nhà (học sinh bấm nút \"Bài tập về nhà\"): link Google Drive / Google Docs."],
            [VideoImportColumns.Category, "Không bắt buộc. Ghi mã chuyên đề (xem sheet ChuyenDe)."],
            [VideoImportColumns.Classes, "Không bắt buộc. Ghi mã lớp (xem sheet Lop), nhiều lớp cách nhau bởi dấu phẩy. Để trống = bài chung, mọi học sinh xem được."],
            [VideoImportColumns.PracticeExam, "Không bắt buộc. Mã đề loại \"Đề luyện\" (xem sheet DeLuyen): học sinh có nút \"Luyện đề\" ở video."],
            ["", ""],
            ["Lưu ý", $"Dòng 1 là tiêu đề, mỗi dòng bên dưới là một video. Tối đa {VideoImportColumns.MaxRows} video mỗi lần. Có dòng lỗi thì không video nào được tạo; sửa theo báo lỗi rồi import lại."],
        ];
        for (var r = 0; r < lines.Length; r++)
        {
            guide.Cell(r + 1, 1).Value = lines[r][0];
            guide.Cell(r + 1, 2).Value = lines[r][1];
        }

        guide.Row(1).Style.Font.Bold = true;
        guide.Column(1).Width = 24;
        guide.Column(2).Width = 120;

        var exampleRow = lines.Length + 3;
        guide.Cell(exampleRow, 1).Value = "Ví dụ cách điền sheet Video:";
        guide.Cell(exampleRow, 1).Style.Font.Bold = true;
        var classCode = classes.Count > 0 ? classes[0].Code : "MA-LOP";
        var categoryCode = categories.Count > 0 ? categories[0].Code : "";
        var examCode = practiceExams.Count > 0 ? practiceExams[0].Code : "";
        string[][] examples =
        [
            [.. headers],
            ["https://www.youtube.com/watch?v=xxxxxxxxxxx", "", "", "", "", "", ""],
            ["https://youtu.be/xxxxxxxxxxx", "", "", "", "", "", ""],
            ["https://www.youtube.com/watch?v=yyyyyyyyyyy", "Bài 1: Hàm số bậc nhất", "https://drive.google.com/file/d/ID_TAI_LIEU/view", "https://drive.google.com/file/d/ID_BTVN/view", categoryCode, classCode, examCode],
        ];
        for (var r = 0; r < examples.Length; r++)
        {
            for (var c = 0; c < examples[r].Length; c++)
            {
                guide.Cell(exampleRow + 1 + r, c + 1).Value = examples[r][c];
            }
        }

        var exampleHeader = guide.Range(exampleRow + 1, 1, exampleRow + 1, headers.Count);
        exampleHeader.Style.Font.Bold = true;
        exampleHeader.Style.Fill.BackgroundColor = XLColor.LightGray;

        AddLookup(workbook, "Lop", "Mã lớp", "Tên lớp", classes);
        AddLookup(workbook, "ChuyenDe", "Mã chuyên đề", "Tên chuyên đề", categories);
        AddLookup(workbook, "DeLuyen", "Mã đề luyện", "Tên đề", practiceExams);
        sheet.SetTabActive();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static void AddLookup(XLWorkbook workbook, string name, string codeHeader, string nameHeader, IReadOnlyList<(string Code, string Name)> items)
    {
        var lookup = workbook.Worksheets.Add(name);
        lookup.Cell(1, 1).Value = codeHeader;
        lookup.Cell(1, 2).Value = nameHeader;
        lookup.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < items.Count; i++)
        {
            lookup.Cell(i + 2, 1).Value = items[i].Code;
            lookup.Cell(i + 2, 2).Value = items[i].Name;
        }

        lookup.Columns().AdjustToContents();
    }
}

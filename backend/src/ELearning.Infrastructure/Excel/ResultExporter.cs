using ClosedXML.Excel;
using ELearning.Application.Admin;
using ELearning.Application.Common.Options;
using ELearning.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Excel;

/// <summary>Export kết quả ra .xlsx; thời gian hiển thị theo múi giờ nghiệp vụ (docs/06-frontend.md mục 5).</summary>
internal sealed class ResultExporter(IOptions<AppOptions> appOptions) : IResultExporter
{
    private static readonly string[] Headers =
    [
        "STT", "Tên đăng nhập", "Họ tên", "Email", "Lượt", "Trạng thái", "Bắt đầu", "Nộp bài", "Thời gian làm (phút)",
        "Điểm", "Điểm tối đa", "Phần trăm", "Số câu đúng", "Tổng số câu", "Kết quả", "Điểm chính thức",
    ];

    public byte[] Export(string examCode, string examName, IReadOnlyList<AdminResultRowDto> rows)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(appOptions.Value.BusinessTimeZone);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Kết quả");

        sheet.Cell(1, 1).Value = $"Kết quả đề {examCode} — {examName}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Thời gian theo múi giờ {appOptions.Value.BusinessTimeZone}";

        const int headerRow = 4;
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = Headers[c];
        }

        sheet.Row(headerRow).Style.Font.Bold = true;
        sheet.Row(headerRow).Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var row = headerRow + 1 + i;
            sheet.Cell(row, 1).Value = i + 1;
            sheet.Cell(row, 2).Value = r.UserName;
            sheet.Cell(row, 3).Value = r.FullName;
            sheet.Cell(row, 4).Value = r.Email;
            sheet.Cell(row, 5).Value = r.AttemptNumber;
            sheet.Cell(row, 6).Value = StatusLabel(r.Status);
            sheet.Cell(row, 7).Value = TimeZoneInfo.ConvertTimeFromUtc(r.StartedAt, zone);
            sheet.Cell(row, 8).Value = TimeZoneInfo.ConvertTimeFromUtc(r.SubmittedAt, zone);
            sheet.Cell(row, 9).Value = Math.Round(r.DurationSeconds / 60m, 1);
            sheet.Cell(row, 10).Value = r.TotalScore;
            sheet.Cell(row, 11).Value = r.MaxScore;
            sheet.Cell(row, 12).Value = r.Percentage / 100m;
            sheet.Cell(row, 13).Value = r.CorrectCount;
            sheet.Cell(row, 14).Value = r.TotalQuestion;
            sheet.Cell(row, 15).Value = r.Passed switch { true => "Đạt", false => "Không đạt", _ => string.Empty };
            sheet.Cell(row, 16).Value = r.IsOfficial ? "✓" : string.Empty;
        }

        if (rows.Count > 0)
        {
            var first = headerRow + 1;
            var last = headerRow + rows.Count;
            sheet.Range(first, 7, last, 8).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Range(first, 12, last, 12).Style.NumberFormat.Format = "0.00%";
        }

        sheet.Columns().AdjustToContents(headerRow, headerRow + Math.Min(rows.Count, 200));
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string StatusLabel(AttemptStatus status) => status switch
    {
        AttemptStatus.Submitted => "Đã nộp",
        AttemptStatus.AutoSubmitted => "Tự động nộp",
        AttemptStatus.Cancelled => "Đã hủy",
        _ => "Đang làm",
    };
}

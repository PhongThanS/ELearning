using System.Globalization;
using ClosedXML.Excel;
using ELearning.Application.Attendance;
using ELearning.Domain.Enums;

namespace ELearning.Infrastructure.Excel;

/// <summary>Xuất điểm danh ra .xlsx (D-31): sheet buổi học đang xem + sheet tổng hợp tháng của buổi đó.</summary>
internal sealed class AttendanceExporter : IAttendanceExporter
{
    private static readonly string[] SessionHeaders =
        ["STT", "Tên đăng nhập", "Họ tên", "Phụ huynh", "SĐT phụ huynh", "Điểm danh", "BTVN", "Điểm BTVN", "Nhận xét"];

    public byte[] Export(AttendanceSessionDto session, AttendanceMonthSheet month)
    {
        using var workbook = new XLWorkbook();
        WriteSession(workbook.Worksheets.Add("Buổi học"), session);
        WriteMonth(workbook.Worksheets.Add($"Tháng {month.Month:00}-{month.Year}"), session, month);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string StatusLabel(AttendanceStatus? status) => status switch
    {
        AttendanceStatus.Present => "Có mặt",
        AttendanceStatus.Late => "Đi muộn",
        AttendanceStatus.Excused => "Vắng có phép",
        AttendanceStatus.Unexcused => "Vắng không phép",
        _ => "Chưa điểm danh",
    };

    private static string HomeworkLabel(HomeworkStatus? status) => status switch
    {
        HomeworkStatus.Excellent => "Xuất sắc",
        HomeworkStatus.Done => "Đạt",
        HomeworkStatus.Partial => "Làm thiếu",
        HomeworkStatus.Missing => "Không làm",
        _ => string.Empty,
    };

    private static string StatusCode(AttendanceStatus? status) => status switch
    {
        AttendanceStatus.Present => "C",
        AttendanceStatus.Late => "M",
        AttendanceStatus.Excused => "P",
        AttendanceStatus.Unexcused => "K",
        _ => string.Empty,
    };

    private static void WriteSession(IXLWorksheet sheet, AttendanceSessionDto session)
    {
        sheet.Cell(1, 1).Value = $"Điểm danh lớp {session.ClassroomCode} — {session.ClassroomName} — ngày {session.SessionDate:dd/MM/yyyy}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Chủ đề: {session.Topic}";
        sheet.Cell(3, 1).Value = $"BTVN buổi sau: {session.NextHomework}";

        const int headerRow = 5;
        WriteHeader(sheet, headerRow, SessionHeaders);
        for (var i = 0; i < session.Students.Count; i++)
        {
            var s = session.Students[i];
            var row = headerRow + 1 + i;
            sheet.Cell(row, 1).Value = i + 1;
            sheet.Cell(row, 2).Value = s.UserName;
            sheet.Cell(row, 3).Value = s.FullName;
            sheet.Cell(row, 4).Value = s.ParentName;
            sheet.Cell(row, 5).SetValue(s.ParentPhone ?? string.Empty);
            sheet.Cell(row, 6).Value = StatusLabel(s.Status);
            sheet.Cell(row, 7).Value = HomeworkLabel(s.HomeworkStatus);
            sheet.Cell(row, 8).Value = s.HomeworkScore;
            sheet.Cell(row, 9).Value = s.Note;
            sheet.Cell(row, 6).Style.Fill.BackgroundColor = s.Status switch
            {
                AttendanceStatus.Present => XLColor.FromHtml("#D1FAE5"),
                AttendanceStatus.Late => XLColor.FromHtml("#FEF3C7"),
                AttendanceStatus.Excused => XLColor.FromHtml("#DBEAFE"),
                AttendanceStatus.Unexcused => XLColor.FromHtml("#FFE4E6"),
                _ => XLColor.NoColor,
            };
        }

        var summaryRow = headerRow + session.Students.Count + 2;
        sheet.Cell(summaryRow, 2).Value = "Tổng kết";
        sheet.Cell(summaryRow, 2).Style.Font.Bold = true;
        var counts = new (string Label, int Count)[]
        {
            ("Sĩ số", session.Students.Count),
            ("Có mặt", session.Students.Count(s => s.Status == AttendanceStatus.Present)),
            ("Đi muộn", session.Students.Count(s => s.Status == AttendanceStatus.Late)),
            ("Vắng có phép", session.Students.Count(s => s.Status == AttendanceStatus.Excused)),
            ("Vắng không phép", session.Students.Count(s => s.Status == AttendanceStatus.Unexcused)),
        };
        for (var i = 0; i < counts.Length; i++)
        {
            sheet.Cell(summaryRow + 1 + i, 2).Value = counts[i].Label;
            sheet.Cell(summaryRow + 1 + i, 3).Value = counts[i].Count;
        }

        var scores = session.Students.Where(s => s.HomeworkScore is not null).Select(s => s.HomeworkScore!.Value).ToList();
        sheet.Cell(summaryRow + 1 + counts.Length, 2).Value = "Điểm BTVN trung bình";
        sheet.Cell(summaryRow + 1 + counts.Length, 3).Value = scores.Count > 0 ? Math.Round(scores.Average(), 2) : Blank.Value;

        sheet.Columns().AdjustToContents(headerRow, headerRow + Math.Min(session.Students.Count, 200));
        sheet.Column(9).Width = Math.Min(sheet.Column(9).Width, 60);
        sheet.SheetView.FreezeRows(headerRow);
    }

    private static void WriteMonth(IXLWorksheet sheet, AttendanceSessionDto session, AttendanceMonthSheet month)
    {
        sheet.Cell(1, 1).Value = $"Tổng hợp tháng {month.Month:00}/{month.Year} — lớp {session.ClassroomCode} — {session.ClassroomName}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = "Mỗi ô: điểm danh (C = có mặt, M = muộn, P = vắng có phép, K = vắng không phép) / điểm BTVN";

        const int headerRow = 4;
        var headers = new List<string> { "STT", "Tên đăng nhập", "Họ tên" };
        headers.AddRange(month.Dates.Select(d => d.ToString("dd/MM", CultureInfo.InvariantCulture)));
        headers.AddRange(["Có mặt", "Đi muộn", "Vắng P", "Vắng K", "ĐTB BTVN"]);
        WriteHeader(sheet, headerRow, [.. headers]);

        for (var i = 0; i < month.Rows.Count; i++)
        {
            var r = month.Rows[i];
            var row = headerRow + 1 + i;
            sheet.Cell(row, 1).Value = i + 1;
            sheet.Cell(row, 2).Value = r.UserName;
            sheet.Cell(row, 3).Value = r.FullName;
            for (var d = 0; d < r.Cells.Count; d++)
            {
                var cell = r.Cells[d];
                var code = StatusCode(cell?.Status);
                var score = cell?.HomeworkScore is { } s ? s.ToString("0.#", CultureInfo.InvariantCulture) : string.Empty;
                sheet.Cell(row, 4 + d).Value = score.Length > 0 ? $"{code}/{score}" : code;
            }

            var col = 4 + month.Dates.Count;
            var cells = r.Cells.OfType<AttendanceHistoryItemDto>().ToList();
            sheet.Cell(row, col).Value = cells.Count(c => c.Status == AttendanceStatus.Present);
            sheet.Cell(row, col + 1).Value = cells.Count(c => c.Status == AttendanceStatus.Late);
            sheet.Cell(row, col + 2).Value = cells.Count(c => c.Status == AttendanceStatus.Excused);
            sheet.Cell(row, col + 3).Value = cells.Count(c => c.Status == AttendanceStatus.Unexcused);
            var scores = cells.Where(c => c.HomeworkScore is not null).Select(c => c.HomeworkScore!.Value).ToList();
            sheet.Cell(row, col + 4).Value = scores.Count > 0 ? Math.Round(scores.Average(), 2) : Blank.Value;
        }

        sheet.Columns().AdjustToContents(headerRow, headerRow + Math.Min(month.Rows.Count, 200));
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(3);
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(row, c + 1).Value = headers[c];
        }

        sheet.Row(row).Style.Font.Bold = true;
        sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
    }
}

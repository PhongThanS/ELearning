using System.Globalization;
using ClosedXML.Excel;
using ELearning.Application.Questions;

namespace ELearning.Infrastructure.Excel;

/// <summary>
/// Đọc file import dạng bảng: dòng đầu tiên có dữ liệu là tiêu đề, các dòng dưới là dữ liệu (bỏ dòng trống).
/// Chỉ trích giá trị ô thành chuỗi theo tiêu đề đã chuẩn hóa; việc diễn giải nằm ở Application.
/// </summary>
internal static class ExcelSheetReader
{
    /// <param name="sheetName">Sheet dữ liệu ưu tiên; không có thì đọc sheet đầu tiên.</param>
    /// <param name="itemName">Tên một dòng trong thông báo vượt giới hạn, ví dụ "câu hỏi", "tài khoản".</param>
    public static QuestionImportSheet Read(Stream stream, string sheetName, int maxRows, string itemName)
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
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
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
                    throw new InvalidDataException($"Mỗi lần import tối đa {maxRows} {itemName}. Hãy tách file.");
                }

                rows.Add(new QuestionImportRawRow(r, cells));
            }

            return new QuestionImportSheet(rows, columns.Values.ToList());
        }
    }

    /// <summary>Giá trị ô dạng chuỗi; ô số ghi theo dấu chấm (không phụ thuộc culture), ô trống → null.</summary>
    public static string? Text(IXLCell cell)
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

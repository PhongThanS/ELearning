using ClosedXML.Excel;
using ELearning.Application.Questions;
using ELearning.Application.Users;

namespace ELearning.Infrastructure.Excel;

/// <summary>Đọc file import tài khoản và tạo file mẫu (kèm hướng dẫn, danh sách vai trò và mã lớp).</summary>
internal sealed class UserImportWorkbook : IUserImportWorkbook
{
    public const string DataSheetName = "TaiKhoan";

    public QuestionImportSheet Read(Stream stream, int maxRows) => ExcelSheetReader.Read(stream, DataSheetName, maxRows, "tài khoản");

    public byte[] CreateTemplate(IReadOnlyList<(string Code, string Name)> roles, IReadOnlyList<(string Code, string Name)> classes)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(DataSheetName);
        var headers = UserImportColumns.TemplateHeaders;
        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);

        // Sheet dữ liệu chỉ có tiêu đề: dòng ví dụ nằm ở sheet HuongDan để không bị import nhầm thành tài khoản thật.
        // Cột chữ: mật khẩu / tên đăng nhập toàn số không bị Excel đổi thành số (mất số 0 đầu)
        sheet.Range(2, 1, UserImportColumns.MaxRows + 1, headers.Count).Style.NumberFormat.Format = "@";
        // Cột Vai trò chọn số trong danh sách 0 / 1 / 2
        var roleColumn = headers.ToList().IndexOf(UserImportColumns.Roles) + 1;
        var roleNumbers = string.Join(',', UserImportColumns.RoleNumbers.Select(r => r.Number));
        var roleValidation = sheet.Range(2, roleColumn, UserImportColumns.MaxRows + 1, roleColumn).CreateDataValidation();
        roleValidation.List($"\"{roleNumbers}\"", true);
        roleValidation.InputTitle = "Vai trò";
        roleValidation.InputMessage = UserImportColumns.RoleNumbersText;
        roleValidation.ErrorMessage = $"Chỉ ghi số: {UserImportColumns.RoleNumbersText}.";
        sheet.Cell(1, roleColumn).GetComment().AddText(UserImportColumns.RoleNumbersText + ". Để trống = Học viên.");
        sheet.Columns().AdjustToContents(1, 4, 12, 40);

        var guide = workbook.Worksheets.Add("HuongDan");
        string[][] lines =
        [
            ["Cột", "Cách ghi"],
            [UserImportColumns.UserName, "Bắt buộc. 3–50 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang. Không trùng tài khoản đã có."],
            [UserImportColumns.Password, "Không bắt buộc. 8–128 ký tự, không trùng tên đăng nhập. Để trống: hệ thống sinh mật khẩu tạm 12 ký tự, hiện ở kết quả import (chỉ một lần). Người dùng tự đăng nhập rồi đổi mật khẩu khi muốn."],
            [UserImportColumns.FullName, "Bắt buộc. Tối đa 200 ký tự."],
            [UserImportColumns.Email, $"Không bắt buộc. Không trùng tài khoản đã có. Để trống: dùng <tên đăng nhập>@{UserImportColumns.PlaceholderEmailDomain}."],
            [UserImportColumns.Roles, $"Không bắt buộc. Ghi số: {UserImportColumns.RoleNumbersText} (xem sheet VaiTro). Để trống = 2 (Học viên)."],
            [UserImportColumns.Classes, "Không bắt buộc. Ghi mã lớp (xem sheet Lop), không ghi tên lớp; nhiều lớp cách nhau bởi dấu phẩy. Chỉ xếp lớp cho Học viên (vai trò 2); cần quyền quản lý lớp học."],
            ["", ""],
            ["Lưu ý", $"Dòng 1 là tiêu đề, mỗi dòng bên dưới là một tài khoản. Tối đa {UserImportColumns.MaxRows} tài khoản mỗi lần. Có dòng lỗi thì không tài khoản nào được tạo; sửa theo báo lỗi rồi import lại."],
        ];
        for (var r = 0; r < lines.Length; r++)
        {
            guide.Cell(r + 1, 1).Value = lines[r][0];
            guide.Cell(r + 1, 2).Value = lines[r][1];
        }

        guide.Row(1).Style.Font.Bold = true;
        guide.Column(1).Width = 24;
        guide.Column(2).Width = 120;

        // Ví dụ cách điền (chép sang sheet TaiKhoan nếu muốn), đặt bên dưới bảng hướng dẫn
        var exampleRow = lines.Length + 3;
        guide.Cell(exampleRow, 1).Value = "Ví dụ cách điền sheet TaiKhoan:";
        guide.Cell(exampleRow, 1).Style.Font.Bold = true;
        var classCode = classes.Count > 0 ? classes[0].Code : "MA-LOP";
        string[][] examples =
        [
            [.. headers],
            ["hs.nguyenvana", "MatKhau@2026", "Nguyễn Văn A", "", "2", classCode],
            ["hs.tranthib", "", "Trần Thị B", "tranthib@example.com", "", classCode],
            ["gv.lethic", "GiaoVien@2026", "Lê Thị C", "lethic@example.com", "1", ""],
            ["admin.pham", "QuanTri@2026", "Phạm Văn D", "", "0", ""],
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

        AddLookup(workbook, "VaiTro", "Số (ghi vào cột Vai trò)", "Vai trò", roles);
        AddLookup(workbook, "Lop", "Mã lớp", "Tên lớp", classes);
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

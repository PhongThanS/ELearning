using ELearning.Application.Questions;
using ELearning.Domain.Identity;

namespace ELearning.Application.Users;

/// <summary>
/// Tên cột của file import tài khoản (docs/02-nghiep-vu.md mục 5.2). Cột nhận theo tiêu đề (không phân biệt hoa thường,
/// bỏ khoảng trắng thừa) nên thứ tự cột không quan trọng.
/// </summary>
public static class UserImportColumns
{
    public const string UserName = "Tên đăng nhập";
    public const string Password = "Mật khẩu";
    public const string FullName = "Họ tên";
    public const string Email = "Email";
    public const string Roles = "Vai trò";
    public const string Classes = "Lớp";

    /// <summary>Cột "Vai trò" ghi bằng số; để trống = Học viên.</summary>
    public static IReadOnlyList<(int Number, string Code, string Label)> RoleNumbers { get; } =
    [
        (0, Role.AdminCode, "Quản trị viên"),
        (1, Role.TeacherCode, "Giáo viên"),
        (2, Role.StudentCode, "Học viên"),
    ];

    /// <summary>"0 = Quản trị viên, 1 = Giáo viên, 2 = Học viên" cho thông báo lỗi và hướng dẫn.</summary>
    public static string RoleNumbersText { get; } = string.Join(", ", RoleNumbers.Select(r => $"{r.Number} = {r.Label}"));

    /// <summary>Số tài khoản tối đa mỗi lần import (băm mật khẩu tốn CPU).</summary>
    public const int MaxRows = 500;

    /// <summary>Email để trống → <c>{tên đăng nhập}@elearning.local</c> (cùng quy ước với tài khoản seed).</summary>
    public const string PlaceholderEmailDomain = "elearning.local";

    public static IReadOnlyList<string> TemplateHeaders { get; } =
        [UserName, Password, FullName, Email, Roles, Classes];

    public static string Normalize(string header) => QuestionImportColumns.Normalize(header);
}

/// <summary>Đọc / tạo file Excel import tài khoản (Infrastructure, ClosedXML).</summary>
public interface IUserImportWorkbook
{
    /// <summary>Đọc sheet dữ liệu; ném <see cref="InvalidDataException"/> nếu không phải file .xlsx hợp lệ.</summary>
    QuestionImportSheet Read(Stream stream, int maxRows);

    byte[] CreateTemplate(IReadOnlyList<(string Code, string Name)> roles, IReadOnlyList<(string Code, string Name)> classes);
}

/// <summary>
/// Kết quả một dòng. <see cref="TemporaryPassword"/> chỉ có sau khi import thật và dòng để trống mật khẩu
/// (hệ thống sinh mật khẩu tạm; người dùng tự đổi sau khi đăng nhập).
/// </summary>
public sealed record UserImportRowResult(
    int RowNumber,
    string? UserName,
    string? FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Classes,
    string? TemporaryPassword,
    IReadOnlyList<QuestionImportIssue> Issues);

/// <summary>
/// Kết quả import tài khoản. Tất cả hoặc không: chỉ cần một dòng lỗi thì không tạo tài khoản nào
/// (<see cref="Imported"/> = false) và <see cref="Rows"/> cho biết lỗi từng dòng.
/// </summary>
public sealed record UserImportResult(
    bool DryRun,
    bool Imported,
    int TotalRows,
    int ValidRows,
    int ImportedCount,
    IReadOnlyList<UserImportRowResult> Rows);

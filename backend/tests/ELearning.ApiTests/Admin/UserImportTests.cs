using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Classes;

namespace ELearning.ApiTests.Admin;

public sealed record UserImportIssueView(string? Field, string Code, string Message);

public sealed record UserImportRowView(
    int RowNumber, string? UserName, string? FullName, List<string> Roles, List<string> Classes, string? TemporaryPassword,
    List<UserImportIssueView> Issues);

public sealed record UserImportView(bool DryRun, bool Imported, int TotalRows, int ValidRows, int ImportedCount, List<UserImportRowView> Rows);

/// <summary>Import tài khoản từ Excel: dòng 1 là tiêu đề, mỗi dòng dưới là một tài khoản; tất cả hoặc không.</summary>
[Collection(ApiCollection.Name)]
public class UserImportTests(ApiFactory factory)
{
    private static readonly string[] Headers = ["Tên đăng nhập", "Mật khẩu", "Họ tên", "Email", "Vai trò", "Lớp"];

    [Fact]
    public async Task Template_has_the_columns()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);

        var response = await kit.Admin.GetAsync(new Uri("/api/users/import/template", UriKind.Relative));
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sheet = workbook.Worksheet("TaiKhoan");
        Enumerable.Range(1, Headers.Length).Select(c => sheet.Cell(1, c).GetString()).Should().Equal(Headers);
        sheet.LastRowUsed()!.RowNumber().Should().Be(1, "sheet dữ liệu chỉ có tiêu đề: ví dụ ở sheet HuongDan, không bị import nhầm");
        workbook.Worksheets.Select(s => s.Name).Should().Contain(["HuongDan", "VaiTro", "Lop"]);
    }

    [Fact]
    public async Task Rows_are_created_with_roles_classes_and_temporary_password()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var classroom = await CreateClassAsync(kit.Admin);
        var (student, blank, teacher, admin) = (Unique("hs."), Unique("hs."), Unique("gv."), Unique("ad."));
        var file = Xlsx(
            [student, "MatKhau@2026", "Nguyễn Văn A", "", "2", classroom.Code.ToLowerInvariant()],
            [blank, "", "Trần Thị B", "", "", classroom.Code],
            [teacher, "GiaoVien@2026", "Lê Thị C", $"{teacher}@example.com", "1", ""],
            [admin, "QuanTri@2026", "Phạm Văn D", "", "0", ""]);

        var (check, checkBody) = await ImportAsync(kit.Admin, file, dryRun: true);
        var (import, importBody) = await ImportAsync(kit.Admin, file, dryRun: false);
        import.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(" | ", importBody.Errors.Select(e => e.Code + ": " + e.Message)));
        importBody.Data!.Imported.Should().BeTrue(string.Join(" | ", importBody.Data.Rows.SelectMany(r => r.Issues).Select(i => i.Field + "/" + i.Code + ": " + i.Message)));
        var result = importBody.Data!;
        var (_, members) = await kit.Admin.GetJsonAsync<Paged<JsonElement>>($"/api/classes/{classroom.Id}/students?pageSize=100");
        var studentLogin = await factory.CreateHttpsClient().LoginAsync(student, "MatKhau@2026");
        var blankLogin = await factory.CreateHttpsClient().LoginAsync(blank, result.Rows[1].TemporaryPassword!);
        var teacherLogin = await factory.CreateHttpsClient().LoginAsync(teacher, "GiaoVien@2026");
        var adminLogin = await factory.CreateHttpsClient().LoginAsync(admin, "QuanTri@2026");

        check.StatusCode.Should().Be(HttpStatusCode.OK);
        checkBody.Data!.Should().Match<UserImportView>(r => r.DryRun && !r.Imported && r.ValidRows == 4 && r.ImportedCount == 0);
        import.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Imported.Should().BeTrue();
        result.ImportedCount.Should().Be(4);
        result.Rows[0].TemporaryPassword.Should().BeNull("đã ghi mật khẩu");
        result.Rows[1].TemporaryPassword.Should().HaveLength(12, "để trống mật khẩu thì hệ thống sinh mật khẩu tạm");
        result.Rows[0].Classes.Should().ContainSingle(classroom.Name);
        members.Data!.Items.Select(m => m.GetProperty("userName").GetString()).Should().BeEquivalentTo([student, blank]);
        // Không bắt đổi mật khẩu: người dùng tự đổi sau khi đăng nhập, kể cả với mật khẩu tạm
        new[] { studentLogin, blankLogin, teacherLogin, adminLogin }.Should().OnlyContain(l => !l.User.MustChangePassword);
        studentLogin.User.Roles.Should().Equal("STUDENT");
        blankLogin.User.Roles.Should().Equal(["STUDENT"], "để trống vai trò = 2 (Học viên)");
        teacherLogin.User.Roles.Should().Equal("TEACHER");
        adminLogin.User.Roles.Should().Equal("ADMIN");
    }

    [Fact]
    public async Task Any_invalid_row_blocks_the_whole_file_and_reports_each_problem()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var good = Unique("hs.");
        var file = Xlsx(
            [good, "MatKhau@2026", "Hợp lệ", "", "", ""],
            [ApiFactory.AdminUserName, "MatKhau@2026", "Trùng tài khoản có sẵn", "", "", ""],
            [good.ToUpperInvariant(), "MatKhau@2026", "Trùng trong file", $"{Guid.NewGuid():N}@example.com", "", ""],
            [Unique("x."), "ngan", "", "", "3", "KHONG-CO-LOP"],
            [Unique("gv."), "", "Giáo viên xếp lớp", "", "1", "KHONG-CO-LOP"]);

        var (response, body) = await ImportAsync(kit.Admin, file, dryRun: false);
        response.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(" | ", body.Errors.Select(e => e.Code + ": " + e.Message)));
        var rows = body.Data!.Rows;
        var (_, after) = await kit.Admin.GetJsonAsync<Paged<JsonElement>>($"/api/users?keyword={good}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Imported.Should().BeFalse();
        body.Data.ValidRows.Should().Be(1);
        rows[0].Issues.Should().BeEmpty();
        rows[1].Issues.Should().Contain(i => i.Code == "USERNAME_TAKEN" && i.Field == "Tên đăng nhập");
        rows[2].Issues.Should().Contain(i => i.Code == "DUPLICATE_IN_FILE" && i.Field == "Tên đăng nhập");
        rows[3].Issues.Select(i => i.Code).Should().Contain(["PASSWORD_TOO_SHORT", "FULLNAME_REQUIRED", "ROLE_NOT_FOUND", "CLASSROOM_NOT_FOUND"]);
        rows[4].Issues.Select(i => i.Code).Should().Contain("CLASSROOM_STUDENT_ONLY");
        after.Data!.TotalCount.Should().Be(0, "có dòng lỗi thì không tạo tài khoản nào");
    }

    [Fact]
    public async Task Wrong_file_and_missing_columns_are_rejected_and_students_cannot_import()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var noFullName = Build(["Tên đăng nhập"], [[Unique("hs.")]]);

        var (notExcel, notExcelBody) = await PostAsync(kit.Admin, "không phải excel"u8.ToArray(), "a.xlsx");
        var (missing, missingBody) = await PostAsync(kit.Admin, noFullName, "a.xlsx");
        var (forbidden, _) = await PostAsync(student, Xlsx([Unique("hs."), "MatKhau@2026", "A", "", "", ""]), "a.xlsx");

        notExcel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notExcelBody.Errors.Should().Contain(e => e.Code == "IMPORT_FILE_INVALID");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingBody.Errors.Should().Contain(e => e.Code == "IMPORT_COLUMNS_MISSING");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Unique(string prefix) => ApiClientExtensions.UniqueName(prefix).ToLowerInvariant();

    /// <summary>File .xlsx: dòng 1 là tiêu đề chuẩn, các dòng sau là dữ liệu (mọi ô ghi dạng chữ).</summary>
    private static byte[] Xlsx(params string[][] rows) => Build(Headers, rows);

    private static byte[] Build(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("TaiKhoan");
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static Task<(HttpResponseMessage Response, Envelope<UserImportView> Body)> ImportAsync(HttpClient client, byte[] file, bool dryRun) =>
        PostAsync(client, file, "tai-khoan.xlsx", dryRun: dryRun);

    private static async Task<(HttpResponseMessage Response, Envelope<UserImportView> Body)> PostAsync(
        HttpClient client, byte[] bytes, string fileName, bool dryRun = false)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        var response = await client.PostAsync(new Uri($"/api/users/import?dryRun={dryRun.ToString().ToLowerInvariant()}", UriKind.Relative), form);
        var text = await response.Content.ReadAsStringAsync();
        var body = string.IsNullOrEmpty(text)
            ? new Envelope<UserImportView>(response.IsSuccessStatusCode, default, null, [], null)
            : JsonSerializer.Deserialize<Envelope<UserImportView>>(text, ApiClientExtensions.Json)!;
        return (response, body);
    }

    private static async Task<ClassItem> CreateClassAsync(HttpClient admin)
    {
        var (response, body) = await admin.PostJsonAsync<ClassItem>("/api/classes", new { code = ApiClientExtensions.UniqueName("CL-"), name = "Lớp import" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        return body.Data!;
    }
}

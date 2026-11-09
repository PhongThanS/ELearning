using System.Globalization;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Questions;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Users;

public interface IUserImportService
{
    Task<Result<UserImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct);

    Task<byte[]> CreateTemplateAsync(CancellationToken ct);
}

/// <summary>
/// Import tài khoản từ Excel (docs/02-nghiep-vu.md mục 5.2). Mỗi dòng đi qua đúng validator của chức năng tạo user;
/// trùng tên đăng nhập / email (với DB hoặc trong file) bị chặn. Tất cả hoặc không: có dòng lỗi thì không tạo tài khoản nào.
/// </summary>
internal sealed class UserImportService(
    IAppDbContext db,
    IUserImportWorkbook workbook,
    IPasswordHasher passwordHasher,
    IUserAccessService userAccess,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateUserRequest> validator) : IUserImportService
{
    private static readonly char[] ListSeparators = [',', ';', '|', '\n'];

    /// <summary>Tên trường của validator → tên cột trong file.</summary>
    private static readonly Dictionary<string, string> FieldColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["userName"] = UserImportColumns.UserName,
        ["email"] = UserImportColumns.Email,
        ["fullName"] = UserImportColumns.FullName,
        ["password"] = UserImportColumns.Password,
    };

    public async Task<Result<UserImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct)
    {
        QuestionImportSheet sheet;
        try
        {
            sheet = workbook.Read(file, UserImportColumns.MaxRows);
        }
        catch (InvalidDataException ex)
        {
            return Error.Validation("IMPORT_FILE_INVALID", ex.Message, "file");
        }

        var missing = new[] { UserImportColumns.UserName, UserImportColumns.FullName }
            .Where(h => !sheet.Headers.Contains(UserImportColumns.Normalize(h)))
            .ToList();
        if (missing.Count > 0)
        {
            return Error.Validation("IMPORT_COLUMNS_MISSING", $"File thiếu cột: {string.Join(", ", missing)}. Hãy dùng file mẫu.", "file");
        }

        if (sheet.Rows.Count == 0)
        {
            return Error.Validation("IMPORT_EMPTY", "File không có dòng tài khoản nào.", "file");
        }

        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive).Select(r => new RoleRef(r.Id, r.Code, r.Name)).ToListAsync(ct);
        var rows = sheet.Rows.Select(raw => Map(raw, roles)).ToList();

        await ResolveClassesAsync(rows, ct);
        await CheckDuplicatesAsync(rows, ct);
        foreach (var row in rows.Where(r => r.Request is not null))
        {
            foreach (var error in await validator.ValidateToErrorsAsync(row.Request!, ct))
            {
                row.Issues.Add(new QuestionImportIssue(ColumnFor(error.Field), error.Code, error.Message));
            }
        }

        var validRows = rows.Count(r => r.Issues.Count == 0);
        if (dryRun || validRows != rows.Count)
        {
            return new UserImportResult(dryRun, Imported: false, rows.Count, validRows, 0, rows.Select(r => r.ToResult(null)).ToList());
        }

        var now = time.GetUtcNow().UtcDateTime;
        var users = rows.Select(r => new User(r.Request!.UserName.Trim(), r.Request.Email.Trim(), r.Request.FullName.Trim(), now)).ToList();
        var temporaryPasswords = rows.Select(r => r.Request!.Password is null ? PasswordRules.GenerateTemporary() : null).ToList();

        // Băm mật khẩu (PBKDF2) song song: phần tốn thời gian nhất của import
        var hashes = new string[rows.Count];
        Parallel.For(0, rows.Count, new ParallelOptions { CancellationToken = ct }, i =>
            hashes[i] = passwordHasher.Hash(users[i], rows[i].Request!.Password ?? temporaryPasswords[i]!));

        await db.InTransactionAsync(
            async () =>
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    // Không bắt đổi mật khẩu: người dùng tự đăng nhập rồi chủ động đổi (kể cả mật khẩu tạm)
                    users[i].SetPassword(hashes[i], mustChangePassword: false, now);
                    users[i].SetRoles(row.Request!.RoleIds!, now);
                    db.Users.Add(users[i]);
                }

                var classIds = rows.SelectMany(r => r.ClassIds).Distinct().ToList();
                var classrooms = await db.Classrooms.Include(c => c.Students).Where(c => classIds.Contains(c.Id)).ToListAsync(ct);
                foreach (var classroom in classrooms)
                {
                    var members = rows.Select((r, i) => (r, i)).Where(x => x.r.ClassIds.Contains(classroom.Id)).Select(x => users[x.i].Id);
                    classroom.AddStudents(members, now);
                }

                audit.Write(
                    AuditActions.UsersImported,
                    nameof(User),
                    newValue: new
                    {
                        Count = users.Count,
                        UserNames = users.Select(u => u.UserName).ToList(),
                        ClassroomIds = classIds,
                    });
                await db.SaveChangesAsync(ct);
                return users.Count;
            },
            ct);

        var results = rows.Select((r, i) => r.ToResult(temporaryPasswords[i])).ToList();
        return new UserImportResult(DryRun: false, Imported: true, rows.Count, validRows, users.Count, results);
    }

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        var classes = await db.Classrooms.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code).Take(2000)
            .Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        var roles = UserImportColumns.RoleNumbers
            .Select(r => (r.Number.ToString(CultureInfo.InvariantCulture), $"{r.Label} ({r.Code})"))
            .ToList();
        return workbook.CreateTemplate(roles, classes.Select(c => (c.Code, c.Name)).ToList());
    }

    /// <summary>Chuyển một dòng chuỗi thành yêu cầu tạo user; vai trò ghi bằng số (<see cref="UserImportColumns.RoleNumbers"/>).</summary>
    private static ImportRow Map(QuestionImportRawRow raw, List<RoleRef> roles)
    {
        var row = new ImportRow(raw.RowNumber);
        string? Get(string column) => raw.Cells.TryGetValue(UserImportColumns.Normalize(column), out var v) ? v.Trim() : null;

        var userName = Get(UserImportColumns.UserName) ?? string.Empty;
        var fullName = Get(UserImportColumns.FullName) ?? string.Empty;
        var email = Get(UserImportColumns.Email)
            ?? (userName.Length > 0 ? $"{userName}@{UserImportColumns.PlaceholderEmailDomain}" : string.Empty);
        var password = Get(UserImportColumns.Password);

        var roleIds = new List<Guid>();
        foreach (var token in Split(Get(UserImportColumns.Roles)))
        {
            var code = UserImportColumns.RoleNumbers.FirstOrDefault(n => n.Number.ToString(CultureInfo.InvariantCulture) == token).Code;
            var role = code is null ? null : roles.Find(r => r.Code == code);
            if (role is null)
            {
                row.Issues.Add(new QuestionImportIssue(
                    UserImportColumns.Roles, "ROLE_NOT_FOUND", $"Vai trò \"{token}\" không hợp lệ. Ghi số: {UserImportColumns.RoleNumbersText}."));
            }
            else if (!roleIds.Contains(role.Id))
            {
                roleIds.Add(role.Id);
                row.RoleNames.Add(role.Name);
                row.IsStudent |= role.Code == Role.StudentCode;
            }
        }

        if (roleIds.Count == 0 && row.Issues.Count == 0 && roles.Find(r => r.Code == Role.StudentCode) is { } student)
        {
            roleIds.Add(student.Id);
            row.RoleNames.Add(student.Name);
            row.IsStudent = true;
        }

        row.ClassCodes.AddRange(Split(Get(UserImportColumns.Classes)).Distinct(StringComparer.OrdinalIgnoreCase));

        row.Request = new CreateUserRequest(userName, email, fullName, password, roleIds, null);
        return row;
    }

    /// <summary>Cột "Lớp" ghi mã lớp (nhiều lớp cách nhau dấu phẩy); chỉ xếp lớp cho tài khoản có vai trò Học viên.</summary>
    private async Task ResolveClassesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        var withClasses = rows.Where(r => r.ClassCodes.Count > 0).ToList();
        if (withClasses.Count == 0)
        {
            return;
        }

        // Xếp lớp là thao tác của quyền quản lý lớp, không chỉ quyền tạo user
        var access = await userAccess.GetAsync(currentUser.RequiredUserId, ct);
        var canManageClasses = access?.Permissions.Contains(Permissions.ClassManage) == true;

        // Mã lớp so không phân biệt hoa thường: ToLower() trong truy vấn được dịch thành lower() của PostgreSQL
        // (ToLowerInvariant không dịch được), nên culture của .NET không tham gia.
        var codes = withClasses.SelectMany(r => r.ClassCodes).Select(c => c.ToLowerInvariant()).Distinct().ToList();
#pragma warning disable CA1304, CA1311
        var found = await db.Classrooms.AsNoTracking()
            .Where(c => codes.Contains(c.Code.ToLower()))
            .Select(c => new { c.Id, c.Code, c.Name, c.IsActive })
            .ToListAsync(ct);
#pragma warning restore CA1304, CA1311
        var byCode = found.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var row in withClasses)
        {
            if (!canManageClasses)
            {
                row.Issues.Add(new QuestionImportIssue(
                    UserImportColumns.Classes, ErrorCodes.Forbidden, "Bạn không có quyền quản lý lớp học nên không xếp lớp được. Để trống cột Lớp."));
                continue;
            }

            if (!row.IsStudent)
            {
                row.Issues.Add(new QuestionImportIssue(
                    UserImportColumns.Classes, "CLASSROOM_STUDENT_ONLY", "Chỉ xếp lớp cho tài khoản có vai trò Học viên."));
            }

            foreach (var code in row.ClassCodes)
            {
                if (byCode.TryGetValue(code, out var classroom) && classroom.IsActive)
                {
                    row.ClassIds.Add(classroom.Id);
                    row.ClassNames.Add(classroom.Name);
                }
                else
                {
                    row.Issues.Add(new QuestionImportIssue(
                        UserImportColumns.Classes, "CLASSROOM_NOT_FOUND", $"Không có lớp đang hoạt động với mã \"{code}\" (xem sheet Lop)."));
                }
            }
        }
    }

    /// <summary>Tên đăng nhập / email không được trùng tài khoản đã có, cũng không trùng nhau trong file.</summary>
    private async Task CheckDuplicatesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        var withRequest = rows.Where(r => r.Request is not null).ToList();
        var userKeys = withRequest.Where(r => r.Request!.UserName.Length > 0).Select(r => User.NormalizeKey(r.Request!.UserName)).Distinct().ToList();
        var emailKeys = withRequest.Where(r => r.Request!.Email.Length > 0).Select(r => User.NormalizeKey(r.Request!.Email)).Distinct().ToList();
        var takenUsers = (await db.Users.AsNoTracking().Where(u => userKeys.Contains(u.NormalizedUserName))
            .Select(u => u.NormalizedUserName).ToListAsync(ct)).ToHashSet();
        var takenEmails = (await db.Users.AsNoTracking().Where(u => emailKeys.Contains(u.NormalizedEmail))
            .Select(u => u.NormalizedEmail).ToListAsync(ct)).ToHashSet();
        var seenUsers = new HashSet<string>();
        var seenEmails = new HashSet<string>();

        foreach (var row in withRequest)
        {
            var (userName, email) = (row.Request!.UserName, row.Request.Email);
            if (userName.Length > 0)
            {
                var key = User.NormalizeKey(userName);
                if (takenUsers.Contains(key))
                {
                    row.Issues.Add(new QuestionImportIssue(UserImportColumns.UserName, ErrorCodes.UserNameTaken, $"Tên đăng nhập \"{userName}\" đã được sử dụng."));
                }
                else if (!seenUsers.Add(key))
                {
                    row.Issues.Add(new QuestionImportIssue(UserImportColumns.UserName, "DUPLICATE_IN_FILE", $"Tên đăng nhập \"{userName}\" bị trùng trong file."));
                }
            }

            if (email.Length > 0)
            {
                var key = User.NormalizeKey(email);
                if (takenEmails.Contains(key))
                {
                    row.Issues.Add(new QuestionImportIssue(UserImportColumns.Email, ErrorCodes.EmailTaken, $"Email \"{email}\" đã được sử dụng."));
                }
                else if (!seenEmails.Add(key))
                {
                    row.Issues.Add(new QuestionImportIssue(UserImportColumns.Email, "DUPLICATE_IN_FILE", $"Email \"{email}\" bị trùng trong file."));
                }
            }
        }
    }

    private static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ColumnFor(string? field) =>
        field is not null && FieldColumns.TryGetValue(field, out var column) ? column : field;

    private sealed record RoleRef(Guid Id, string Code, string Name);

    private sealed class ImportRow(int rowNumber)
    {
        public int RowNumber { get; } = rowNumber;

        public CreateUserRequest? Request { get; set; }

        public bool IsStudent { get; set; }

        public List<string> RoleNames { get; } = [];

        public List<string> ClassCodes { get; } = [];

        public List<Guid> ClassIds { get; } = [];

        public List<string> ClassNames { get; } = [];

        public List<QuestionImportIssue> Issues { get; } = [];

        public UserImportRowResult ToResult(string? temporaryPassword) =>
            new(RowNumber, Request?.UserName, Request?.FullName, RoleNames, ClassNames, temporaryPassword, Issues);
    }
}

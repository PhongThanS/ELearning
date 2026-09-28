using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Persistence.Seed;

/// <summary>Section "Seed". Mật khẩu lấy từ cấu hình / secret store, không hard-code trong code.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUserName { get; init; } = "admin";

    public string AdminEmail { get; init; } = "admin@elearning.local";

    /// <summary>Chỉ dùng khi chưa có tài khoản ADMIN nào. Production: bắt buộc đổi ở lần đăng nhập đầu.</summary>
    public string? AdminPassword { get; init; }

    /// <summary>Mật khẩu cho tài khoản demo (chỉ Development).</summary>
    public string? DevPassword { get; init; }
}

/// <summary>
/// Seed idempotent (docs/09-van-hanh.md mục 3):
/// - Core (mọi môi trường): permission, vai trò hệ thống, ADMIN có mọi quyền, tài khoản admin khởi tạo.
/// - Development: tài khoản demo và nhóm DEMO.
/// </summary>
public sealed class DatabaseSeeder(
    ELearningDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider time,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    public const string DemoGroupCode = "DEMO";

    private readonly SeedOptions _options = options.Value;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task SeedAsync(bool includeDevelopmentData, CancellationToken ct = default)
    {
        await SeedPermissionsAndRolesAsync(ct);
        var admin = await SeedAdminAsync(mustChangePassword: !includeDevelopmentData, ct);
        if (includeDevelopmentData && admin is not null)
        {
            await SeedDevelopmentAsync(admin.Id, ct);
        }
    }

    private async Task SeedPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.Ordinal, ct);
        foreach (var (code, name) in Permissions.All.Where(kv => !existing.ContainsKey(kv.Key)))
        {
            db.Permissions.Add(new Permission(code, name));
        }

        await EnsureRoleAsync(Role.AdminCode, "Quản trị viên", ct);
        await EnsureRoleAsync(Role.StudentCode, "Học viên", ct);
        await db.SaveChangesAsync(ct);

        // ADMIN luôn có mọi permission (kể cả permission mới thêm)
        var adminRole = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Code == Role.AdminCode, ct);
        var allIds = await db.Permissions.Select(p => p.Id).ToListAsync(ct);
        if (adminRole.Permissions.Count != allIds.Count)
        {
            adminRole.SetPermissions(allIds);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureRoleAsync(string code, string name, CancellationToken ct)
    {
        if (!await db.Roles.AnyAsync(r => r.Code == code, ct))
        {
            db.Roles.Add(new Role(code, name, isSystem: true));
        }
    }

    private async Task<User?> SeedAdminAsync(bool mustChangePassword, CancellationToken ct)
    {
        var existingAdmin = await db.Users
            .Where(u => u.Roles.Any(r => r.Role.Code == Role.AdminCode))
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (existingAdmin is not null)
        {
            return existingAdmin;
        }

        if (string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            logger.LogWarning("Chưa có tài khoản ADMIN và thiếu Seed:AdminPassword; bỏ qua tạo admin khởi tạo.");
            return null;
        }

        var adminRoleId = await db.Roles.Where(r => r.Code == Role.AdminCode).Select(r => r.Id).SingleAsync(ct);
        var admin = new User(_options.AdminUserName, _options.AdminEmail, "Quản trị viên", Now);
        admin.SetPassword(passwordHasher.Hash(admin, _options.AdminPassword), mustChangePassword, Now);
        admin.SetRoles([adminRoleId], Now);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã tạo tài khoản admin khởi tạo {UserName}", admin.UserName);
        return admin;
    }

    private async Task SeedDevelopmentAsync(Guid adminId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.DevPassword))
        {
            logger.LogWarning("Thiếu Seed:DevPassword; bỏ qua tài khoản demo.");
            return;
        }

        var studentRoleId = await db.Roles.Where(r => r.Code == Role.StudentCode).Select(r => r.Id).SingleAsync(ct);
        var students = new List<User>();
        foreach (var (userName, fullName) in new[] { ("student01", "Nguyễn Văn An"), ("student02", "Trần Thị Bình") })
        {
            var key = User.NormalizeKey(userName);
            var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == key, ct);
            if (user is null)
            {
                user = new User(userName, $"{userName}@elearning.local", fullName, Now);
                user.SetPassword(passwordHasher.Hash(user, _options.DevPassword), mustChangePassword: false, Now);
                user.SetRoles([studentRoleId], Now);
                db.Users.Add(user);
            }

            students.Add(user);
        }

        var group = await db.UserGroups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Code == DemoGroupCode, ct);
        if (group is null)
        {
            group = new UserGroup(DemoGroupCode, "Nhóm demo", "Nhóm học viên mẫu cho môi trường phát triển", adminId, Now);
            db.UserGroups.Add(group);
        }

        group.AddMembers(students.Select(s => s.Id), Now);
        await db.SaveChangesAsync(ct);
    }
}

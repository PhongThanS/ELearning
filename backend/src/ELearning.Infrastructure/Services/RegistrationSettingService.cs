using ELearning.Application.Audit;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Services;

public sealed class RegistrationSettingService(
    IAppDbContext db,
    IOptions<AuthOptions> authOptions,
    IAuditService audit,
    TimeProvider time,
    ILogger<RegistrationSettingService> logger) : IRegistrationSettingService
{
    public const string RegistrationKey = "Auth.AllowSelfRegistration";
    // 0: unset, 1: false, 2: true
    private static int _cachedState;
    private static bool _tableEnsured;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableEnsured)
        {
            return;
        }

        await Lock.WaitAsync(ct);
        try
        {
            if (_tableEnsured)
            {
                return;
            }

            const string sql = """
                CREATE TABLE IF NOT EXISTS "SystemSettings" (
                    "Key" VARCHAR(100) PRIMARY KEY,
                    "Value" TEXT NOT NULL,
                    "Description" VARCHAR(500) NULL,
                    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
                    "UpdatedBy" UUID NULL
                );
                """;
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            _tableEnsured = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể kiểm tra/tạo bảng SystemSettings tự động.");
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken ct = default)
    {
        var state = Volatile.Read(ref _cachedState);
        if (state != 0)
        {
            return state == 2;
        }

        await EnsureTableAsync(ct);

        try
        {
            var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == RegistrationKey, ct);
            if (setting is not null && bool.TryParse(setting.Value, out var parsed))
            {
                Volatile.Write(ref _cachedState, parsed ? 2 : 1);
                return parsed;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi truy vấn SystemSettings từ CSDL, dùng giá trị mặc định từ cấu hình.");
        }

        var defaultValue = authOptions.Value.AllowSelfRegistration;
        Volatile.Write(ref _cachedState, defaultValue ? 2 : 1);
        return defaultValue;
    }

    public async Task<bool> SetSelfRegistrationAllowedAsync(bool allow, Guid? adminId, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;

        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.Key == RegistrationKey, ct);
        if (setting is null)
        {
            setting = new SystemSetting(
                RegistrationKey,
                allow ? "true" : "false",
                "Cho phép học sinh tự đăng ký tài khoản trên giao diện web",
                now,
                adminId);
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.UpdateValue(allow ? "true" : "false", now, adminId);
        }

        audit.Write("REGISTRATION_POLICY_CHANGED", "SystemSetting", Guid.Empty, newValue: new { AllowSelfRegistration = allow }, userId: adminId);
        await db.SaveChangesAsync(ct);

        Volatile.Write(ref _cachedState, allow ? 2 : 1);
        logger.LogInformation("Quản trị viên {AdminId} đã {Action} chức năng tự đăng ký.", adminId, allow ? "BẬT" : "TẮT");
        return allow;
    }
}

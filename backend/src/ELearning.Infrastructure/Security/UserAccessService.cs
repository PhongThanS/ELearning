using ELearning.Application.Common.Abstractions;
using ELearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELearning.Infrastructure.Security;

/// <summary>
/// Cache ảnh chụp quyền của user 5 phút (D-14). Chỉ đúng khi chạy một instance;
/// khi scale ngang phải chuyển sang cache dùng chung (docs/09-van-hanh.md mục 5).
/// </summary>
internal sealed class UserAccessService(IMemoryCache cache, ELearningDbContext db) : IUserAccessService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    // Tăng khi quyền của một vai trò thay đổi → vô hiệu hóa cache của mọi user.
    private static long _generation;

    public async Task<UserAccessSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId);
        if (cache.TryGetValue(key, out UserAccessSnapshot? cached))
        {
            return cached;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp, u.MustChangePassword })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.Role.Code)
            .ToListAsync(cancellationToken);
        var permissions = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .SelectMany(ur => ur.Role.Permissions.Select(p => p.Permission.Code))
            .Distinct()
            .ToListAsync(cancellationToken);

        var snapshot = new UserAccessSnapshot(
            userId,
            user.IsActive,
            user.SecurityStamp,
            user.MustChangePassword,
            roles.ToHashSet(StringComparer.Ordinal),
            permissions.ToHashSet(StringComparer.Ordinal));

        cache.Set(key, snapshot, Ttl);
        return snapshot;
    }

    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

    public void InvalidateAll() => Interlocked.Increment(ref _generation);

    private static string CacheKey(Guid userId) => $"user-access:{Interlocked.Read(ref _generation)}:{userId:N}";
}

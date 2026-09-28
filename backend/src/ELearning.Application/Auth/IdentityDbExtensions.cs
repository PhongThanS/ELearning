using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Auth;

internal static class IdentityDbExtensions
{
    /// <summary>Trả về lỗi 409 nếu username/email đã được dùng (so theo khóa chuẩn hóa).</summary>
    public static async Task<Error?> FindDuplicateUserAsync(
        this IAppDbContext db, string? userName, string? email, Guid? excludeUserId, CancellationToken ct)
    {
        if (userName is not null)
        {
            var userKey = User.NormalizeKey(userName);
            if (await db.Users.AnyAsync(u => u.NormalizedUserName == userKey && u.Id != excludeUserId, ct))
            {
                return Error.Conflict(ErrorCodes.UserNameTaken, "Tên đăng nhập đã được sử dụng.");
            }
        }

        if (email is not null)
        {
            var emailKey = User.NormalizeKey(email);
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == emailKey && u.Id != excludeUserId, ct))
            {
                return Error.Conflict(ErrorCodes.EmailTaken, "Email đã được sử dụng.");
            }
        }

        return null;
    }

    /// <summary>Thu hồi mọi refresh token còn hiệu lực của user (chưa SaveChanges).</summary>
    public static async Task RevokeAllRefreshTokensAsync(
        this IAppDbContext db, Guid userId, RefreshTokenRevokedReason reason, DateTime now, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        tokens.ForEach(t => t.Revoke(reason, now));
    }

    /// <summary>Mã vai trò và permission còn hiệu lực của user, đã sắp xếp.</summary>
    public static async Task<(List<string> Roles, List<string> Permissions)> LoadRolesAndPermissionsAsync(
        this IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.Role.Code)
            .ToListAsync(ct);

        var permissions = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .SelectMany(ur => ur.Role.Permissions.Select(p => p.Permission.Code))
            .Distinct()
            .ToListAsync(ct);

        roles.Sort(StringComparer.Ordinal);
        permissions.Sort(StringComparer.Ordinal);
        return (roles, permissions);
    }
}

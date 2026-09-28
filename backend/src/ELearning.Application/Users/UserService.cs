using ELearning.Application.Audit;
using ELearning.Application.Auth;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken ct);

    Task<Result<UserDetailDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<UserWithPasswordDto>> CreateAsync(CreateUserRequest request, CancellationToken ct);

    Task<Result<UserDetailDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);

    Task<Result<UserDetailDto>> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct);

    Task<Result<UserDetailDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct);

    Task<Result<UserWithPasswordDto>> ResetPasswordAsync(Guid id, CancellationToken ct);

    Task<Result<UserDetailDto>> AnonymizeAsync(Guid id, ReasonRequest request, CancellationToken ct);
}

/// <summary>Quản trị người dùng (docs/05-api.md mục 6.2).</summary>
internal sealed class UserService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IUserAccessService userAccess,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<ReasonRequest> reasonValidator) : IUserService
{
    private static readonly Error UserNotFound = Error.NotFound(message: "Không tìm thấy người dùng.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            users = users.Where(u => EF.Functions.Like(u.UserName, kw) || EF.Functions.Like(u.Email, kw)
                || EF.Functions.Like(u.FullName, kw));
        }

        if (!string.IsNullOrWhiteSpace(query.RoleCode))
        {
            var roleCode = query.RoleCode.Trim().ToUpperInvariant();
            users = users.Where(u => u.Roles.Any(r => r.Role.Code == roleCode));
        }

        if (query.GroupId is { } groupId)
        {
            users = users.Where(u => db.UserGroupMembers.Any(m => m.GroupId == groupId && m.UserId == u.Id));
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        users = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("fullname", false) => users.OrderBy(u => u.FullName),
            ("fullname", true) => users.OrderByDescending(u => u.FullName),
            ("email", false) => users.OrderBy(u => u.Email),
            ("email", true) => users.OrderByDescending(u => u.Email),
            ("createdat", false) => users.OrderBy(u => u.CreatedAt),
            ("createdat", true) => users.OrderByDescending(u => u.CreatedAt),
            ("lastloginat", false) => users.OrderBy(u => u.LastLoginAt),
            ("lastloginat", true) => users.OrderByDescending(u => u.LastLoginAt),
            (_, true) => users.OrderByDescending(u => u.UserName),
            _ => users.OrderBy(u => u.UserName),
        };

        var total = await users.CountAsync(ct);
        var now = Now;
        var items = await users
            .Skip(query.Skip).Take(query.PageSize)
            .Select(u => new UserListItemDto(
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.IsActive,
                u.LockoutEnd != null && u.LockoutEnd > now,
                u.Roles.Select(r => r.Role.Code).OrderBy(c => c).ToList(),
                u.LastLoginAt,
                u.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<UserListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<UserDetailDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? UserNotFound : await ToDetailAsync(user, ct);
    }

    public async Task<Result<UserWithPasswordDto>> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<UserWithPasswordDto>.Failure(errors);
        }

        if (await db.FindDuplicateUserAsync(request.UserName, request.Email, null, ct) is { } duplicate)
        {
            return duplicate;
        }

        var roleIds = request.RoleIds is { Count: > 0 }
            ? request.RoleIds.Distinct().ToList()
            : [await db.Roles.Where(r => r.Code == Role.StudentCode).Select(r => r.Id).SingleAsync(ct)];
        if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id), ct) != roleIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có vai trò không tồn tại.", "roleIds");
        }

        var groupIds = request.GroupIds?.Distinct().ToList() ?? [];
        var groups = await db.UserGroups.Include(g => g.Members).Where(g => groupIds.Contains(g.Id)).ToListAsync(ct);
        if (groups.Count != groupIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có nhóm không tồn tại.", "groupIds");
        }

        var temporaryPassword = request.Password is null ? PasswordRules.GenerateTemporary() : null;
        var user = new User(request.UserName, request.Email, request.FullName, Now);
        user.SetPassword(passwordHasher.Hash(user, request.Password ?? temporaryPassword!), temporaryPassword is not null, Now);
        user.SetRoles(roleIds, Now);
        db.Users.Add(user);
        groups.ForEach(g => g.AddMembers([user.Id], Now));

        audit.Write(
            AuditActions.UserCreated,
            nameof(User),
            user.Id,
            newValue: new { user.UserName, user.Email, user.FullName, RoleIds = roleIds, GroupIds = groupIds });
        await db.SaveChangesAsync(ct);

        return new UserWithPasswordDto(await ToDetailAsync(user, ct), temporaryPassword);
    }

    public async Task<Result<UserDetailDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<UserDetailDto>.Failure(errors);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return UserNotFound;
        }

        if (!db.TryApplyRowVersion(user, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        if (await db.FindDuplicateUserAsync(null, request.Email, id, ct) is { } duplicate)
        {
            return duplicate;
        }

        var old = new { user.Email, user.FullName };
        user.UpdateProfile(request.Email, request.FullName, Now);
        audit.Write(AuditActions.UserUpdated, nameof(User), user.Id, old, new { user.Email, user.FullName });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(user, ct);
    }

    public async Task<Result<UserDetailDto>> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct)
    {
        if (id == currentUser.UserId)
        {
            return Error.Business(ErrorCodes.CannotModifySelf, "Không thể thay đổi trạng thái tài khoản của chính mình.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return UserNotFound;
        }

        var wasActive = user.IsActive;
        user.SetActive(request.IsActive, Now);
        if (!request.IsActive)
        {
            await db.RevokeAllRefreshTokensAsync(user.Id, RefreshTokenRevokedReason.UserDisabled, Now, ct);
        }

        if (wasActive != request.IsActive)
        {
            audit.Write(
                AuditActions.UserStatusChanged,
                nameof(User),
                user.Id,
                new { IsActive = wasActive },
                new { request.IsActive },
                request.Reason);
        }

        await db.SaveChangesAsync(ct);
        userAccess.Invalidate(user.Id);
        return await ToDetailAsync(user, ct);
    }

    public async Task<Result<UserDetailDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct)
    {
        var roleIds = request.RoleIds.Distinct().ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != roleIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có vai trò không tồn tại.", "roleIds");
        }

        var user = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return UserNotFound;
        }

        if (id == currentUser.UserId && user.Roles.Any(r => r.Role.Code == Role.AdminCode)
            && roles.TrueForAll(r => r.Code != Role.AdminCode))
        {
            return Error.Business(ErrorCodes.CannotModifySelf, "Không thể tự gỡ vai trò ADMIN của chính mình.");
        }

        var oldCodes = user.Roles.Select(r => r.Role.Code).Order(StringComparer.Ordinal).ToList();
        user.SetRoles(roleIds, Now);
        await db.RevokeAllRefreshTokensAsync(user.Id, RefreshTokenRevokedReason.Admin, Now, ct);
        audit.Write(
            AuditActions.UserRolesChanged,
            nameof(User),
            user.Id,
            new { Roles = oldCodes },
            new { Roles = roles.Select(r => r.Code).Order(StringComparer.Ordinal).ToList() });
        await db.SaveChangesAsync(ct);
        userAccess.Invalidate(user.Id);
        return await ToDetailAsync(user, ct);
    }

    public async Task<Result<UserWithPasswordDto>> ResetPasswordAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return UserNotFound;
        }

        if (user.IsAnonymized)
        {
            return Error.Business(ErrorCodes.UserAnonymized, "Người dùng đã bị ẩn danh hóa.");
        }

        var temporaryPassword = PasswordRules.GenerateTemporary();
        user.SetPassword(passwordHasher.Hash(user, temporaryPassword), mustChangePassword: true, Now);
        await db.RevokeAllRefreshTokensAsync(user.Id, RefreshTokenRevokedReason.Admin, Now, ct);
        audit.Write(AuditActions.PasswordResetByAdmin, nameof(User), user.Id);
        await db.SaveChangesAsync(ct);
        userAccess.Invalidate(user.Id);
        return new UserWithPasswordDto(await ToDetailAsync(user, ct), temporaryPassword);
    }

    public async Task<Result<UserDetailDto>> AnonymizeAsync(Guid id, ReasonRequest request, CancellationToken ct)
    {
        var errors = await reasonValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<UserDetailDto>.Failure(errors);
        }

        if (id == currentUser.UserId)
        {
            return Error.Business(ErrorCodes.CannotModifySelf, "Không thể ẩn danh hóa tài khoản của chính mình.");
        }

        var user = await db.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return UserNotFound;
        }

        if (user.IsAnonymized)
        {
            return Error.Business(ErrorCodes.UserAnonymized, "Người dùng đã bị ẩn danh hóa.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            user.Anonymize(Now);
            await db.RevokeAllRefreshTokensAsync(user.Id, RefreshTokenRevokedReason.UserDisabled, Now, ct);
            audit.Write(AuditActions.UserAnonymized, nameof(User), user.Id, reason: request.Reason);
            await db.SaveChangesAsync(ct);

            // Xóa dấu vết IP / user agent (docs/07-bao-mat.md mục 11)
            await db.RefreshTokens.Where(t => t.UserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedByIp, (string?)null).SetProperty(t => t.UserAgent, (string?)null), ct);
            await db.ExamAttempts.Where(a => a.UserId == id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(a => a.StartedIp, (string?)null)
                        .SetProperty(a => a.SubmittedIp, (string?)null)
                        .SetProperty(a => a.StartedUserAgent, (string?)null),
                    ct);
            await db.AttemptEvents.Where(e => db.ExamAttempts.Any(a => a.Id == e.AttemptId && a.UserId == id))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IpAddress, (string?)null), ct);
            await db.AuditLogs.Where(a => a.UserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IpAddress, (string?)null).SetProperty(a => a.UserAgent, (string?)null), ct);

            await tx.CommitAsync(ct);
        });

        userAccess.Invalidate(user.Id);
        return await ToDetailAsync(user, ct);
    }

    private async Task<UserDetailDto> ToDetailAsync(User user, CancellationToken ct)
    {
        var roles = await db.UserRoles.AsNoTracking().Where(ur => ur.UserId == user.Id)
            .Select(ur => new RefDto(ur.Role.Id, ur.Role.Code, ur.Role.Name)).ToListAsync(ct);
        var groups = await db.UserGroupMembers.AsNoTracking().Where(m => m.UserId == user.Id)
            .Join(db.UserGroups, m => m.GroupId, g => g.Id, (m, g) => new RefDto(g.Id, g.Code, g.Name))
            .ToListAsync(ct);

        return new UserDetailDto(
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.IsActive,
            user.MustChangePassword,
            user.LockoutEnd,
            user.LastLoginAt,
            user.AnonymizedAt,
            user.CreatedAt,
            user.UpdatedAt,
            roles.OrderBy(r => r.Code, StringComparer.Ordinal).ToList(),
            groups.OrderBy(g => g.Code, StringComparer.Ordinal).ToList(),
            user.RowVersion.ToBase64());
    }
}

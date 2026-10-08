using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Identity;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Groups;

public sealed record GroupListQuery : PageRequest
{
    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record GroupDto(
    Guid Id, string Code, string Name, string? Description, bool IsActive, int MemberCount, DateTime CreatedAt, string RowVersion);

public sealed record GroupMemberDto(Guid UserId, string UserName, string FullName, string Email, bool IsActive, DateTime AddedAt);

public sealed record CreateGroupRequest(string Code, string Name, string? Description);

public sealed record UpdateGroupRequest(string Name, string? Description, bool IsActive, string RowVersion);

public sealed record SetStatusRequest(bool IsActive);

public sealed record AddGroupMembersRequest(IReadOnlyList<Guid> UserIds);

public interface IGroupService
{
    Task<PagedResult<GroupDto>> ListAsync(GroupListQuery query, CancellationToken ct);

    Task<Result<GroupDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<GroupDto>> CreateAsync(CreateGroupRequest request, CancellationToken ct);

    Task<Result<GroupDto>> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct);

    Task<Result<GroupDto>> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken ct);

    Task<Result<PagedResult<GroupMemberDto>>> ListMembersAsync(Guid id, UserMemberQuery query, CancellationToken ct);

    Task<Result<GroupDto>> AddMembersAsync(Guid id, AddGroupMembersRequest request, CancellationToken ct);

    Task<Result<GroupDto>> RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed record UserMemberQuery : PageRequest
{
    public string? Keyword { get; init; }
}

internal sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().WithErrorCode("CODE_REQUIRED").WithMessage("Vui lòng nhập mã nhóm.")
            .Matches("^[A-Za-z0-9._-]{2,100}$").WithErrorCode("CODE_INVALID")
            .WithMessage("Mã nhóm gồm 2–100 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang.");
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên nhóm.").MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(1000);
    }
}

internal sealed class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên nhóm.").MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(1000);
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

/// <summary>Nhóm người dùng để gán đề (D-10, docs/05-api.md mục 6.2).</summary>
internal sealed class GroupService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateGroupRequest> createValidator,
    IValidator<UpdateGroupRequest> updateValidator) : IGroupService
{
    private static readonly Error GroupNotFound = Error.NotFound(message: "Không tìm thấy nhóm.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<GroupDto>> ListAsync(GroupListQuery query, CancellationToken ct)
    {
        var groups = db.UserGroups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            groups = groups.Where(g => EF.Functions.Like(g.Code, kw) || EF.Functions.Like(g.Name, kw));
        }

        if (query.IsActive is { } isActive)
        {
            groups = groups.Where(g => g.IsActive == isActive);
        }

        groups = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => groups.OrderBy(g => g.Name),
            ("name", true) => groups.OrderByDescending(g => g.Name),
            ("createdat", false) => groups.OrderBy(g => g.CreatedAt),
            ("createdat", true) => groups.OrderByDescending(g => g.CreatedAt),
            (_, true) => groups.OrderByDescending(g => g.Code),
            _ => groups.OrderBy(g => g.Code),
        };

        var total = await groups.CountAsync(ct);
        var items = await groups.Skip(query.Skip).Take(query.PageSize).Select(ToDto()).ToListAsync(ct);
        return new PagedResult<GroupDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<GroupDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.UserGroups.AsNoTracking().Where(g => g.Id == id).Select(ToDto()).SingleOrDefaultAsync(ct);
        return dto is null ? GroupNotFound : dto;
    }

    public async Task<Result<GroupDto>> CreateAsync(CreateGroupRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<GroupDto>.Failure(errors);
        }

        var code = request.Code.Trim();
        if (await db.UserGroups.AnyAsync(g => g.Code == code, ct))
        {
            return Error.Conflict(ErrorCodes.DuplicateCode, "Mã nhóm đã tồn tại.");
        }

        var group = new UserGroup(code, request.Name, request.Description, currentUser.RequiredUserId, Now);
        db.UserGroups.Add(group);
        audit.Write(AuditActions.GroupCreated, nameof(UserGroup), group.Id, newValue: new { group.Code, group.Name });
        await db.SaveChangesAsync(ct);
        return await GetAsync(group.Id, ct);
    }

    public async Task<Result<GroupDto>> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<GroupDto>.Failure(errors);
        }

        var group = await db.UserGroups.SingleOrDefaultAsync(g => g.Id == id, ct);
        if (group is null)
        {
            return GroupNotFound;
        }

        if (!db.TryApplyRowVersion(group, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        var old = new { group.Name, group.Description, group.IsActive };
        group.Update(request.Name, request.Description, request.IsActive);
        audit.Write(AuditActions.GroupUpdated, nameof(UserGroup), group.Id, old, new { group.Name, group.Description, group.IsActive });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<GroupDto>> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken ct)
    {
        var group = await db.UserGroups.SingleOrDefaultAsync(g => g.Id == id, ct);
        if (group is null)
        {
            return GroupNotFound;
        }

        if (group.IsActive != request.IsActive)
        {
            group.Update(group.Name, group.Description, request.IsActive);
            audit.Write(AuditActions.GroupUpdated, nameof(UserGroup), group.Id, new { IsActive = !request.IsActive }, new { request.IsActive });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<Result<PagedResult<GroupMemberDto>>> ListMembersAsync(Guid id, UserMemberQuery query, CancellationToken ct)
    {
        if (!await db.UserGroups.AnyAsync(g => g.Id == id, ct))
        {
            return GroupNotFound;
        }

        var members = db.UserGroupMembers.AsNoTracking().Where(m => m.GroupId == id)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u });
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            members = members.Where(x => EF.Functions.Like(x.u.UserName, kw) || EF.Functions.Like(x.u.FullName, kw));
        }

        var total = await members.CountAsync(ct);
        var items = await members.OrderBy(x => x.u.UserName).Skip(query.Skip).Take(query.PageSize)
            .Select(x => new GroupMemberDto(x.u.Id, x.u.UserName, x.u.FullName, x.u.Email, x.u.IsActive, x.m.AddedAt))
            .ToListAsync(ct);
        return new PagedResult<GroupMemberDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<GroupDto>> AddMembersAsync(Guid id, AddGroupMembersRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds.Distinct().ToList();
        if (userIds.Count is 0 or > 500)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Danh sách người dùng phải có từ 1 đến 500 phần tử.", "userIds");
        }

        var group = await db.UserGroups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == id, ct);
        if (group is null)
        {
            return GroupNotFound;
        }

        if (await db.Users.CountAsync(u => userIds.Contains(u.Id), ct) != userIds.Count)
        {
            return Error.Validation(ErrorCodes.ValidationFailed, "Có người dùng không tồn tại.", "userIds");
        }

        var added = group.AddMembers(userIds, Now);
        if (added > 0)
        {
            audit.Write(AuditActions.GroupMembersChanged, nameof(UserGroup), group.Id, newValue: new { Added = userIds });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<Result<GroupDto>> RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var group = await db.UserGroups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == id, ct);
        if (group is null)
        {
            return GroupNotFound;
        }

        if (group.RemoveMember(userId))
        {
            audit.Write(AuditActions.GroupMembersChanged, nameof(UserGroup), group.Id, newValue: new { Removed = new[] { userId } });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var group = await db.UserGroups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == id, ct);
        if (group is null)
        {
            return GroupNotFound;
        }

        var assignments = await db.ExamAssignments.Where(a => a.GroupId == id).ToListAsync(ct);
        if (assignments.Count > 0)
        {
            db.ExamAssignments.RemoveRange(assignments);
        }

        if (group.Members.Count > 0)
        {
            db.UserGroupMembers.RemoveRange(group.Members);
        }

        db.UserGroups.Remove(group);
        audit.Write(AuditActions.GroupDeleted, nameof(UserGroup), group.Id, new { group.Code, group.Name }, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static System.Linq.Expressions.Expression<Func<UserGroup, GroupDto>> ToDto() =>
        g => new GroupDto(
            g.Id,
            g.Code,
            g.Name,
            g.Description,
            g.IsActive,
            g.Members.Count,
            g.CreatedAt,
            Convert.ToBase64String(g.RowVersion));
}

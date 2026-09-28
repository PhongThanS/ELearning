using System.Net;
using ELearning.Domain.Identity;

namespace ELearning.ApiTests.Admin;

public sealed record UserDetail(
    Guid Id, string UserName, string Email, string FullName, bool IsActive, bool MustChangePassword,
    List<RefItem> Roles, List<RefItem> Groups, string RowVersion);

public sealed record UserWithPassword(UserDetail User, string? TemporaryPassword);

public sealed record RefItem(Guid Id, string Code, string Name);

public sealed record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record GroupItem(Guid Id, string Code, string Name, bool IsActive, int MemberCount, string RowVersion);

public sealed record RoleItem(Guid Id, string Code, string Name, bool IsSystem, bool IsActive, List<string> Permissions);

public sealed record PermissionItem(Guid Id, string Code, string Name);

/// <summary>Quản trị người dùng / nhóm / vai trò và các quy tắc phân quyền.</summary>
[Collection(ApiCollection.Name)]
public class IdentityAdminTests(ApiFactory factory)
{
    [Fact]
    public async Task Student_cannot_call_admin_apis()
    {
        var student = factory.CreateHttpsClient();
        await student.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (users, usersBody) = await student.GetJsonAsync<object>("/api/users");
        var (roles, _) = await student.GetJsonAsync<object>("/api/roles");

        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        usersBody.Errors.Single().Code.Should().Be("FORBIDDEN");
        roles.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_lists_users_with_paging_and_filters()
    {
        var admin = await AdminAsync();

        var (response, body) = await admin.GetJsonAsync<Paged<object>>("/api/users?roleCode=student&pageSize=500&sortBy=userName");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data!.PageSize.Should().Be(100, "pageSize bị ép tối đa 100");
        body.Data.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Created_user_without_password_gets_temporary_password_and_must_change_it()
    {
        var admin = await AdminAsync();
        var userName = ApiClientExtensions.UniqueName("tmp");

        var (created, createdBody) = await admin.PostJsonAsync<UserWithPassword>(
            "/api/users", new { userName, email = $"{userName}@test.vn", fullName = "Tạm Thời" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var temporary = createdBody.Data!.TemporaryPassword;
        temporary.Should().NotBeNullOrEmpty();
        createdBody.Data.User.MustChangePassword.Should().BeTrue();
        createdBody.Data.User.Roles.Select(r => r.Code).Should().Equal(Role.StudentCode);

        var user = factory.CreateHttpsClient();
        var login = await user.LoginAsync(userName, temporary!);
        login.User.MustChangePassword.Should().BeTrue();

        // Chưa đổi mật khẩu thì mọi API cần policy đều bị chặn với mã riêng
        var (blocked, blockedBody) = await user.GetJsonAsync<object>("/api/users");
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        blockedBody.Errors.Single().Code.Should().Be("PASSWORD_CHANGE_REQUIRED");

        var (changed, changedBody) = await user.PostJsonAsync<LoginData>(
            "/api/auth/change-password", new { currentPassword = temporary, newPassword = "DaDoi@2026" });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        changedBody.Data!.User.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivating_user_invalidates_existing_access_token_immediately()
    {
        var admin = await AdminAsync();
        var (userId, userName, password) = await CreateUserAsync(admin);
        var user = factory.CreateHttpsClient();
        await user.LoginAsync(userName, password);

        var (status, _) = await admin.SendJsonAsync<UserDetail>(
            HttpMethod.Patch, $"/api/users/{userId}/status", new { isActive = false, reason = "Nghỉ học" });
        var (me, meBody) = await user.GetJsonAsync<object>("/api/auth/me");
        var (login, loginBody) = await factory.CreateHttpsClient()
            .PostJsonAsync<object>("/api/auth/login", new { userName, password });

        status.StatusCode.Should().Be(HttpStatusCode.OK);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        meBody.Errors.Single().Code.Should().Be("TOKEN_INVALID");
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        loginBody.Errors.Single().Code.Should().Be("ACCOUNT_DISABLED");
    }

    [Fact]
    public async Task Admin_cannot_deactivate_self_or_remove_own_admin_role()
    {
        var admin = factory.CreateHttpsClient();
        var login = await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        var (roles, rolesBody) = await admin.GetJsonAsync<List<RoleItem>>("/api/roles");
        roles.EnsureSuccessStatusCode();
        var studentRoleId = rolesBody.Data!.Single(r => r.Code == Role.StudentCode).Id;

        var (status, statusBody) = await admin.SendJsonAsync<object>(
            HttpMethod.Patch, $"/api/users/{login.User.Id}/status", new { isActive = false });
        var (setRoles, setRolesBody) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/users/{login.User.Id}/roles", new { roleIds = new[] { studentRoleId } });

        status.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        statusBody.Errors.Single().Code.Should().Be("CANNOT_MODIFY_SELF");
        setRoles.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        setRolesBody.Errors.Single().Code.Should().Be("CANNOT_MODIFY_SELF");
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var (userId, _, _) = await CreateUserAsync(admin);
        var (_, detail) = await admin.GetJsonAsync<UserDetail>($"/api/users/{userId}");
        var staleVersion = detail.Data!.RowVersion;

        var (first, _) = await admin.SendJsonAsync<UserDetail>(
            HttpMethod.Put, $"/api/users/{userId}",
            new { email = detail.Data.Email, fullName = "Tên Mới 1", rowVersion = staleVersion });
        var (second, secondBody) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/users/{userId}",
            new { email = detail.Data.Email, fullName = "Tên Mới 2", rowVersion = staleVersion });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondBody.Errors.Single().Code.Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Reset_password_issues_temporary_password_and_revokes_old_one()
    {
        var admin = await AdminAsync();
        var (userId, userName, password) = await CreateUserAsync(admin);

        var (reset, resetBody) = await admin.PostJsonAsync<UserWithPassword>($"/api/users/{userId}/reset-password");
        var (oldLogin, _) = await factory.CreateHttpsClient().PostJsonAsync<object>("/api/auth/login", new { userName, password });

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var login = await factory.CreateHttpsClient().LoginAsync(userName, resetBody.Data!.TemporaryPassword!);
        login.User.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Anonymized_user_cannot_log_in_and_personal_data_is_removed()
    {
        var admin = await AdminAsync();
        var (userId, userName, password) = await CreateUserAsync(admin);

        var (response, body) = await admin.PostJsonAsync<UserDetail>(
            $"/api/users/{userId}/anonymize", new { reason = "Yêu cầu xóa dữ liệu" });
        var (login, _) = await factory.CreateHttpsClient().PostJsonAsync<object>("/api/auth/login", new { userName, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data!.FullName.Should().Be("Người dùng đã xóa");
        body.Data.UserName.Should().StartWith("deleted-");
        body.Data.IsActive.Should().BeFalse();
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Groups_can_be_created_and_members_managed()
    {
        var admin = await AdminAsync();
        var (userId, _, _) = await CreateUserAsync(admin);
        var code = ApiClientExtensions.UniqueName("LOP-");

        var (created, group) = await admin.PostJsonAsync<GroupItem>("/api/groups", new { code, name = "Lớp thử" });
        var (duplicate, duplicateBody) = await admin.PostJsonAsync<object>("/api/groups", new { code, name = "Trùng" });
        var (added, addedBody) = await admin.PostJsonAsync<GroupItem>(
            $"/api/groups/{group.Data!.Id}/members", new { userIds = new[] { userId, userId } });
        var (members, membersBody) = await admin.GetJsonAsync<Paged<object>>($"/api/groups/{group.Data.Id}/members");
        var (removed, removedBody) = await admin.SendJsonAsync<GroupItem>(
            HttpMethod.Delete, $"/api/groups/{group.Data.Id}/members/{userId}");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicateBody.Errors.Single().Code.Should().Be("DUPLICATE_CODE");
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        addedBody.Data!.MemberCount.Should().Be(1);
        membersBody.Data!.TotalCount.Should().Be(1);
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        removedBody.Data!.MemberCount.Should().Be(0);
        members.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Roles_expose_permissions_and_admin_role_is_immutable()
    {
        var admin = await AdminAsync();

        var (_, permissions) = await admin.GetJsonAsync<List<PermissionItem>>("/api/permissions");
        var (_, roles) = await admin.GetJsonAsync<List<RoleItem>>("/api/roles");
        var adminRole = roles.Data!.Single(r => r.Code == Role.AdminCode);
        var (change, changeBody) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/roles/{adminRole.Id}/permissions", new { permissionIds = Array.Empty<Guid>() });

        permissions.Data!.Select(p => p.Code).Should().BeEquivalentTo(Permissions.All.Keys);
        adminRole.Permissions.Should().HaveCount(Permissions.All.Count);
        change.StatusCode.Should().Be(HttpStatusCode.Conflict);
        changeBody.Errors.Single().Code.Should().Be("SYSTEM_ROLE_IMMUTABLE");
    }

    [Fact]
    public async Task New_role_permissions_take_effect_for_assigned_user()
    {
        var admin = await AdminAsync();
        var (_, permissions) = await admin.GetJsonAsync<List<PermissionItem>>("/api/permissions");
        var userView = permissions.Data!.Single(p => p.Code == Permissions.UserView).Id;
        var code = ApiClientExtensions.UniqueName("VIEWER_").ToUpperInvariant();

        var (createdRole, role) = await admin.PostJsonAsync<RoleItem>(
            "/api/roles", new { code, name = "Người xem", permissionIds = new[] { userView } });
        var (userId, userName, password) = await CreateUserAsync(admin);
        var (assign, _) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/users/{userId}/roles", new { roleIds = new[] { role.Data!.Id } });

        var viewer = factory.CreateHttpsClient();
        await viewer.LoginAsync(userName, password);
        var (canView, _) = await viewer.GetJsonAsync<object>("/api/users");
        var (cannotCreate, _) = await viewer.PostJsonAsync<object>("/api/groups", new { code = "X1", name = "X" });

        createdRole.StatusCode.Should().Be(HttpStatusCode.Created);
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        canView.StatusCode.Should().Be(HttpStatusCode.OK);
        cannotCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }

    private static async Task<(Guid Id, string UserName, string Password)> CreateUserAsync(HttpClient admin)
    {
        var userName = ApiClientExtensions.UniqueName("u");
        const string password = "BanDau@2026";
        var (response, body) = await admin.PostJsonAsync<UserWithPassword>(
            "/api/users", new { userName, email = $"{userName}@test.vn", fullName = "Người Thử", password });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (body.Data!.User.Id, userName, password);
    }
}

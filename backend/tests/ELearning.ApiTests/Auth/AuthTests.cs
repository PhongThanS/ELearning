using System.Net;
using ELearning.Domain.Identity;
using Microsoft.Net.Http.Headers;

namespace ELearning.ApiTests.Auth;

/// <summary>Xác thực (docs/08-kiem-thu.md mục 4, docs/07-bao-mat.md mục 2–3).</summary>
[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_returns_access_token_permissions_and_secure_refresh_cookie()
    {
        var client = factory.CreateHttpsClient();

        var (response, body) = await client.PostJsonAsync<LoginData>(
            "/api/auth/login", new { userName = ApiFactory.AdminUserName, password = ApiFactory.AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.User.Roles.Should().Contain(Role.AdminCode);
        body.Data.User.Permissions.Should().BeEquivalentTo(Permissions.All.Keys);

        var cookie = response.Headers.GetValues(HeaderNames.SetCookie).Single(c => c.StartsWith("elearning_rt=", StringComparison.Ordinal));
        cookie.Should().ContainEquivalentOf("httponly").And.ContainEquivalentOf("secure")
            .And.ContainEquivalentOf("samesite=strict").And.ContainEquivalentOf("path=/api/auth");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("refreshToken", "refresh token chỉ nằm trong cookie");
    }

    [Fact]
    public async Task Login_accepts_email_instead_of_user_name()
    {
        var client = factory.CreateHttpsClient();

        var login = await client.LoginAsync("STUDENT01@elearning.local", ApiFactory.StudentPassword);

        login.User.UserName.Should().Be(ApiFactory.StudentUserName);
    }

    [Fact]
    public async Task Wrong_password_returns_generic_invalid_credentials()
    {
        var client = factory.CreateHttpsClient();

        var (wrongPassword, body1) = await client.PostJsonAsync<object>(
            "/api/auth/login", new { userName = ApiFactory.StudentUserName, password = "sai-mat-khau" });
        var (unknownUser, body2) = await client.PostJsonAsync<object>(
            "/api/auth/login", new { userName = "khong-ton-tai", password = "sai-mat-khau" });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body1.Errors.Single().Code.Should().Be("INVALID_CREDENTIALS");
        body2.Errors.Single().Should().BeEquivalentTo(body1.Errors.Single(), "không để lộ tài khoản có tồn tại hay không");
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_attempts()
    {
        var (userName, password) = await CreateUserAsync();
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < 4; i++)
        {
            var (r, b) = await client.PostJsonAsync<object>("/api/auth/login", new { userName, password = "sai" });
            b.Errors.Single().Code.Should().Be("INVALID_CREDENTIALS");
        }

        var (fifth, fifthBody) = await client.PostJsonAsync<object>("/api/auth/login", new { userName, password = "sai" });
        var (correct, correctBody) = await client.PostJsonAsync<object>("/api/auth/login", new { userName, password });

        fifthBody.Errors.Single().Code.Should().Be("ACCOUNT_LOCKED");
        correct.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        correctBody.Errors.Single().Code.Should().Be("ACCOUNT_LOCKED", "đang bị khóa thì mật khẩu đúng cũng bị từ chối");
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_of_old_token_revokes_family()
    {
        var client = factory.CreateHttpsClient();
        var (_, originalCookie) = await client.LoginWithCookieAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (first, firstBody) = await client.PostJsonAsync<LoginData>("/api/auth/refresh", csrf: true, refreshCookie: originalCookie);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody.Data!.AccessToken.Should().NotBeNullOrEmpty();
        var rotatedCookie = ApiClientExtensions.ReadRefreshCookie(first);
        rotatedCookie.Should().NotBeNullOrEmpty().And.NotBe(originalCookie, "refresh token phải xoay vòng");

        // Quá thời gian ân hạn (D-24) rồi dùng lại token cũ → coi là bị đánh cắp
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        var (reuse, reuseBody) = await factory.CreateHttpsClient()
            .PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: originalCookie);
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        reuseBody.Errors.Single().Code.Should().Be("TOKEN_INVALID");

        // Token hợp lệ của người dùng thật cũng bị thu hồi theo cả chuỗi
        var (afterReuse, _) = await client.PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: rotatedCookie);
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concurrent_refresh_within_grace_period_is_accepted()
    {
        var client = factory.CreateHttpsClient();
        var (_, originalCookie) = await client.LoginWithCookieAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (first, _) = await client.PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: originalCookie);
        var (second, _) = await factory.CreateHttpsClient()
            .PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: originalCookie);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "hai tab refresh gần như cùng lúc không phải là tấn công");
    }

    [Fact]
    public async Task Refresh_without_csrf_header_is_forbidden()
    {
        var client = factory.CreateHttpsClient();
        var (_, cookie) = await client.LoginWithCookieAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (response, body) = await client.PostJsonAsync<object>("/api/auth/refresh", refreshCookie: cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Errors.Single().Code.Should().Be("CSRF_CHECK_FAILED");
    }

    [Fact]
    public async Task Refresh_from_foreign_origin_is_forbidden()
    {
        var client = factory.CreateHttpsClient();
        var (_, cookie) = await client.LoginWithCookieAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example");

        var (response, _) = await client.PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = factory.CreateHttpsClient();
        var (_, cookie) = await client.LoginWithCookieAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (logout, _) = await client.PostJsonAsync<object>("/api/auth/logout", csrf: true, refreshCookie: cookie);
        var (refresh, _) = await factory.CreateHttpsClient()
            .PostJsonAsync<object>("/api/auth/refresh", csrf: true, refreshCookie: cookie);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var anonymous = factory.CreateHttpsClient();
        var (unauthenticated, body) = await anonymous.GetJsonAsync<object>("/api/auth/me");
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Errors.Single().Code.Should().Be("UNAUTHENTICATED");

        var client = factory.CreateHttpsClient();
        await client.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);
        var (me, meBody) = await client.GetJsonAsync<LoginUser>("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        meBody.Data!.Roles.Should().Equal(Role.StudentCode);
        meBody.Data.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Change_password_invalidates_old_access_token_and_returns_new_session()
    {
        var (userName, password) = await CreateUserAsync();
        var client = factory.CreateHttpsClient();
        var login = await client.LoginAsync(userName, password);

        var (changed, changedBody) = await client.PostJsonAsync<LoginData>(
            "/api/auth/change-password", new { currentPassword = password, newPassword = "MatKhauMoi@2026" });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        var old = factory.CreateHttpsClient();
        old.UseToken(login.AccessToken);
        var (oldResponse, oldBody) = await old.GetJsonAsync<object>("/api/auth/me");
        oldResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldBody.Errors.Single().Code.Should().Be("TOKEN_INVALID");

        client.UseToken(changedBody.Data!.AccessToken);
        var (me, _) = await client.GetJsonAsync<object>("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        await factory.CreateHttpsClient().LoginAsync(userName, "MatKhauMoi@2026");
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_fails()
    {
        var (userName, password) = await CreateUserAsync();
        var client = factory.CreateHttpsClient();
        await client.LoginAsync(userName, password);

        var (response, body) = await client.PostJsonAsync<object>(
            "/api/auth/change-password", new { currentPassword = "sai", newPassword = "MatKhauMoi@2026" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        body.Errors.Single().Code.Should().Be("INVALID_CURRENT_PASSWORD");
    }

    [Fact]
    public async Task Register_creates_student_and_rejects_duplicates()
    {
        var client = factory.CreateHttpsClient();
        var userName = ApiClientExtensions.UniqueName("hv");
        var request = new
        {
            userName,
            email = $"{userName}@test.vn",
            fullName = "Học Viên Mới",
            password = "HocVien@2026",
            acceptTerms = true,
        };

        var (created, createdBody) = await client.PostJsonAsync<LoginUser>("/api/auth/register", request);
        var (duplicate, duplicateBody) = await client.PostJsonAsync<object>("/api/auth/register", request);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        createdBody.Data!.Roles.Should().Equal(Role.StudentCode);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicateBody.Errors.Single().Code.Should().Be("USERNAME_TAKEN");
    }

    [Fact]
    public async Task Register_validates_input_with_field_errors()
    {
        var client = factory.CreateHttpsClient();

        var (response, body) = await client.PostJsonAsync<object>(
            "/api/auth/register",
            new { userName = "a b", email = "not-an-email", fullName = "", password = "123", acceptTerms = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Message.Should().Be("Dữ liệu không hợp lệ.");
        body.Errors.Select(e => e.Field).Should().Contain(["userName", "email", "fullName", "password", "acceptTerms"]);
    }

    [Fact]
    public async Task Forgot_password_always_returns_ok()
    {
        var (response, body) = await factory.CreateHttpsClient()
            .PostJsonAsync<object>("/api/auth/forgot-password", new { email = "khong-ton-tai@test.vn" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Message.Should().NotBeNullOrEmpty();
    }

    private async Task<(string UserName, string Password)> CreateUserAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        var userName = ApiClientExtensions.UniqueName("u");
        const string password = "BanDau@2026";
        var (response, _) = await admin.PostJsonAsync<object>(
            "/api/users", new { userName, email = $"{userName}@test.vn", fullName = "Người Thử", password });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (userName, password);
    }
}

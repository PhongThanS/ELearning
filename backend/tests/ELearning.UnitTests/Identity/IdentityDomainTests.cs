using ELearning.Application.Common;
using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;

namespace ELearning.UnitTests.Identity;

public class IdentityDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalized_keys_ignore_case_whitespace_and_unicode_form()
    {
        var composed = "Hà Nội";
        var decomposed = composed.Normalize(System.Text.NormalizationForm.FormD);

        User.NormalizeKey($"  {decomposed} ").Should().Be(User.NormalizeKey(composed.ToUpperInvariant()));
    }

    [Fact]
    public void Lockout_happens_on_the_configured_failed_attempt()
    {
        var user = new User("hv01", "hv01@test.vn", "Học viên", Now);

        for (var i = 1; i < 5; i++)
        {
            user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15)).Should().BeFalse();
        }

        user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15)).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(14)).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).Should().BeFalse();
    }

    [Fact]
    public void Successful_login_resets_failed_counter()
    {
        var user = new User("hv01", "hv01@test.vn", "Học viên", Now);
        user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));

        user.RecordSuccessfulLogin(Now);

        user.AccessFailedCount.Should().Be(0);
        user.LastLoginAt.Should().Be(Now);
    }

    [Fact]
    public void Security_stamp_rotates_on_password_status_and_role_changes()
    {
        var user = new User("hv01", "hv01@test.vn", "Học viên", Now);
        var stamps = new HashSet<Guid> { user.SecurityStamp };

        user.SetPassword("hash", mustChangePassword: false, Now);
        stamps.Add(user.SecurityStamp).Should().BeTrue();

        user.SetActive(false, Now);
        stamps.Add(user.SecurityStamp).Should().BeTrue();

        user.SetRoles([Guid.NewGuid()], Now);
        stamps.Add(user.SecurityStamp).Should().BeTrue();
    }

    [Fact]
    public void Anonymize_removes_personal_data_and_blocks_reactivation()
    {
        var user = new User("hv01", "hv01@test.vn", "Nguyễn Văn A", Now);
        user.SetPassword("hash", false, Now);

        user.Anonymize(Now);

        user.FullName.Should().Be("Người dùng đã xóa");
        user.Email.Should().NotContain("hv01");
        user.PasswordHash.Should().BeNull();
        user.IsActive.Should().BeFalse();
        var reactivate = () => user.SetActive(true, Now);
        reactivate.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.UserAnonymized);
    }

    [Fact]
    public void Set_roles_replaces_existing_roles_without_duplicates()
    {
        var user = new User("hv01", "hv01@test.vn", "Học viên", Now);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        user.SetRoles([a, b, b], Now);

        user.SetRoles([b], Now);

        user.Roles.Select(r => r.RoleId).Should().Equal(b);
    }

    [Fact]
    public void Refresh_token_revocation_is_idempotent_and_keeps_first_reason()
    {
        var token = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(7), null, null);
        token.IsActive(Now).Should().BeTrue();

        token.Revoke(RefreshTokenRevokedReason.Rotated, Now, Guid.NewGuid());
        token.Revoke(RefreshTokenRevokedReason.Logout, Now.AddMinutes(1));

        token.RevokedReason.Should().Be(RefreshTokenRevokedReason.Rotated);
        token.RevokedAt.Should().Be(Now);
        token.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void System_role_cannot_be_deactivated()
    {
        var role = new Role(Role.AdminCode, "Quản trị viên", isSystem: true);

        var act = () => role.Update("Quản trị", isActive: false);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("[x]", "%[[]x]%")]
    [InlineData("  hà nội ", "%hà nội%")]
    public void Like_pattern_escapes_wildcards(string keyword, string expected) =>
        Like.Contains(keyword).Should().Be(expected);

    [Fact]
    public void Temporary_password_is_random_and_readable()
    {
        var a = PasswordRules.GenerateTemporary();
        var b = PasswordRules.GenerateTemporary();

        a.Should().HaveLength(12).And.NotBe(b);
        a.Should().NotContainAny("0", "O", "1", "l", "I");
    }
}

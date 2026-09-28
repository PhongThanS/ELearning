using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ConfigureEntity("Users");
        builder.Property(u => u.UserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();

        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("UQ_Users_NormalizedUserName");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("UQ_Users_NormalizedEmail");

        builder.HasMany(u => u.Roles).WithOne().HasForeignKey(ur => ur.UserId).HasConstraintName("FK_UserRoles_User");
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ConfigureEntity("Roles");
        builder.Property(r => r.Code).IsCode(50);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("UQ_Roles_Code");

        builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(rp => rp.RoleId)
            .HasConstraintName("FK_RolePermissions_Role");
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ConfigureEntity("Permissions");
        builder.Property(p => p.Code).IsCode(100);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("UQ_Permissions_Code");
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId }).HasName("PK_UserRoles");
        builder.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).HasConstraintName("FK_UserRoles_Role");
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId }).HasName("PK_RolePermissions");
        builder.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId)
            .HasConstraintName("FK_RolePermissions_Permission");
    }
}

internal sealed class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> builder)
    {
        builder.ConfigureEntity("UserGroups");
        builder.Property(g => g.Code).IsCode(100);
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(1000);
        builder.HasIndex(g => g.Code).IsUnique().HasDatabaseName("UQ_UserGroups_Code");
        builder.HasUserReference(g => g.CreatedBy, "FK_UserGroups_CreatedBy");

        builder.HasMany(g => g.Members).WithOne().HasForeignKey(m => m.GroupId)
            .HasConstraintName("FK_UserGroupMembers_Group");
    }
}

internal sealed class UserGroupMemberConfiguration : IEntityTypeConfiguration<UserGroupMember>
{
    public void Configure(EntityTypeBuilder<UserGroupMember> builder)
    {
        builder.ToTable("UserGroupMembers");
        builder.HasKey(m => new { m.GroupId, m.UserId }).HasName("PK_UserGroupMembers");
        builder.HasUserReference(m => m.UserId, "FK_UserGroupMembers_User");
        builder.HasIndex(m => m.UserId).HasDatabaseName("IX_UserGroupMembers_User");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ConfigureEntity("RefreshTokens");
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(t => t.CreatedByIp).IsIp();
        builder.Property(t => t.UserAgent).HasMaxLength(500);
        builder.HasUserReference(t => t.UserId, "FK_RefreshTokens_User");

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("UQ_RefreshTokens_TokenHash");
        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_User");
        builder.HasIndex(t => t.FamilyId).HasDatabaseName("IX_RefreshTokens_Family");

        builder.ToTable(t => t.HasEnumCheck<RefreshTokenRevokedReason>("CK_RefreshTokens_RevokedReason", "RevokedReason"));
    }
}

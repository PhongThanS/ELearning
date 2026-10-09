using System.Linq.Expressions;
using ELearning.Domain.Common;
using ELearning.Domain.Identity;
using ELearning.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public const string SqlDecimalAnswer = "decimal(30,10)";
    public const string SqlPercentage = "decimal(5,2)";

    /// <summary>CHECK constraint "\"Column\" IN ('A','B',...)" sinh từ enum (cho phép NULL nếu cột nullable).</summary>
    public static void HasEnumCheck<TEnum>(this TableBuilder table, string constraintName, string column)
        where TEnum : struct, Enum
    {
        var values = string.Join(",", EnumNaming.DbValues(typeof(TEnum)).Select(v => $"'{v}'"));
        table.HasCheckConstraint(constraintName, $"\"{column}\" IN ({values})");
    }

    /// <summary>Cấu hình khóa GUID sinh phía client (sequential) + cột RowVersion nếu có.</summary>
    public static void ConfigureEntity<T>(this EntityTypeBuilder<T> builder, string table)
        where T : Entity
    {
        builder.ToTable(table);
        builder.HasKey(e => e.Id).HasName($"PK_{table}");
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        if (typeof(IHasRowVersion).IsAssignableFrom(typeof(T)))
        {
            builder.Property(nameof(IHasRowVersion.RowVersion))
                .IsConcurrencyToken()
                .IsRequired();
        }
    }

    /// <summary>FK tới Users không có navigation (CreatedBy, UpdatedBy, ...).</summary>
    public static void HasUserReference<T>(
        this EntityTypeBuilder<T> builder, Expression<Func<T, object?>> foreignKey, string constraintName)
        where T : class =>
        builder.HasOne<User>().WithMany().HasForeignKey(foreignKey).HasConstraintName(constraintName);

    public static PropertyBuilder<string> IsCode(this PropertyBuilder<string> property, int maxLength = 100) =>
        property.HasMaxLength(maxLength).IsUnicode(false).IsRequired();

    public static PropertyBuilder<string?> IsIp(this PropertyBuilder<string?> property) =>
        property.HasMaxLength(45).IsUnicode(false);
}

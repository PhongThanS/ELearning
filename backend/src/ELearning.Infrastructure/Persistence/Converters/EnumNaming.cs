using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ELearning.Infrastructure.Persistence.Converters;

/// <summary>
/// Chuyển tên enum PascalCase ↔ UPPER_SNAKE_CASE ("SingleChoice" ↔ "SINGLE_CHOICE"),
/// khớp với JsonNamingPolicy.SnakeCaseUpper dùng ở API (docs/10-bay-ky-thuat.md mục 2).
/// </summary>
public static class EnumNaming
{
    private static readonly ConcurrentDictionary<Type, (Dictionary<Enum, string> ToDb, Dictionary<string, Enum> FromDb)> Cache = new();

    public static string ToSnakeUpper(string pascalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(pascalName);
        var sb = new StringBuilder(pascalName.Length + 8);
        for (var i = 0; i < pascalName.Length; i++)
        {
            var c = pascalName[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(pascalName[i - 1]) || char.IsDigit(pascalName[i - 1])))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    public static string ToDb<TEnum>(TEnum value)
        where TEnum : struct, Enum => Maps(typeof(TEnum)).ToDb[value];

    public static TEnum FromDb<TEnum>(string value)
        where TEnum : struct, Enum =>
        Maps(typeof(TEnum)).FromDb.TryGetValue(value, out var e)
            ? (TEnum)e
            : throw new InvalidOperationException($"Giá trị '{value}' không hợp lệ cho enum {typeof(TEnum).Name}.");

    /// <summary>Mọi giá trị DB hợp lệ của enum, dùng để sinh CHECK constraint.</summary>
    public static IReadOnlyCollection<string> DbValues(Type enumType) => Maps(enumType).ToDb.Values;

    private static (Dictionary<Enum, string> ToDb, Dictionary<string, Enum> FromDb) Maps(Type enumType) =>
        Cache.GetOrAdd(enumType, t =>
        {
            var toDb = Enum.GetValues(t).Cast<Enum>().ToDictionary(e => e, e => ToSnakeUpper(e.ToString()));
            var fromDb = toDb.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
            return (toDb, fromDb);
        });
}

public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(v => EnumNaming.ToDb(v), v => EnumNaming.FromDb<TEnum>(v))
    {
    }
}

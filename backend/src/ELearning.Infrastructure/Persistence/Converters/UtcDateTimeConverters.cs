using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ELearning.Infrastructure.Persistence.Converters;

/// <summary>
/// Ép mọi DateTime đọc từ DB về DateTimeKind.Utc để JSON luôn có hậu tố "Z"
/// (docs/10-bay-ky-thuat.md mục 1).
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue ? UtcDateTimeConverter.ToUtc(v.Value) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}

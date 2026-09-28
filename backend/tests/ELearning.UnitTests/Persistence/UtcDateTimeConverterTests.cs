using ELearning.Infrastructure.Persistence.Converters;

namespace ELearning.UnitTests.Persistence;

public class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();
    private readonly NullableUtcDateTimeConverter _nullableConverter = new();

    [Fact]
    public void Value_read_from_database_is_marked_utc()
    {
        var fromDb = new DateTime(2026, 9, 26, 3, 0, 0, DateTimeKind.Unspecified);

        var result = (DateTime)_converter.ConvertFromProvider(fromDb)!;

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(new DateTime(2026, 9, 26, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Local_time_is_converted_to_utc_before_saving()
    {
        var local = new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Local);

        var result = (DateTime)_converter.ConvertToProvider(local)!;

        result.Should().Be(local.ToUniversalTime());
    }

    [Fact]
    public void Utc_value_is_saved_unchanged()
    {
        var utc = new DateTime(2026, 9, 26, 3, 0, 0, DateTimeKind.Utc);
        ((DateTime)_converter.ConvertToProvider(utc)!).Should().Be(utc);
    }

    [Fact]
    public void Nullable_converter_keeps_null_and_marks_utc()
    {
        _nullableConverter.ConvertFromProvider(null).Should().BeNull();

        var result = (DateTime?)_nullableConverter.ConvertFromProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}

using System.Text.Json;
using ELearning.Domain.Enums;
using ELearning.Infrastructure.Persistence.Converters;

namespace ELearning.UnitTests.Persistence;

public class EnumNamingTests
{
    public static TheoryData<Type> DomainEnums()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(QuestionType).Assembly.GetTypes().Where(t => t.IsEnum && t.Namespace == "ELearning.Domain.Enums"))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [InlineData("SingleChoice", "SINGLE_CHOICE")]
    [InlineData("TrueFalse", "TRUE_FALSE")]
    [InlineData("InProgress", "IN_PROGRESS")]
    [InlineData("AfterLastAttempt", "AFTER_LAST_ATTEMPT")]
    [InlineData("Published", "PUBLISHED")]
    public void ToSnakeUpper_converts_pascal_case(string input, string expected) =>
        EnumNaming.ToSnakeUpper(input).Should().Be(expected);

    [Theory]
    [MemberData(nameof(DomainEnums))]
    public void Db_names_match_api_json_naming(Type enumType)
    {
        // DB (EF converter) và API (JsonNamingPolicy.SnakeCaseUpper) phải dùng đúng cùng một tên.
        foreach (var name in Enum.GetNames(enumType))
        {
            EnumNaming.ToSnakeUpper(name).Should().Be(JsonNamingPolicy.SnakeCaseUpper.ConvertName(name), $"{enumType.Name}.{name}");
        }
    }

    [Theory]
    [MemberData(nameof(DomainEnums))]
    public void Db_names_are_unique_and_fit_column(Type enumType)
    {
        var values = EnumNaming.DbValues(enumType);
        values.Should().OnlyHaveUniqueItems();
        values.Should().AllSatisfy(v => v.Length.Should().BeLessThanOrEqualTo(40));
    }

    [Fact]
    public void Round_trips_through_database_representation()
    {
        foreach (var value in Enum.GetValues<ReviewPolicy>())
        {
            EnumNaming.FromDb<ReviewPolicy>(EnumNaming.ToDb(value)).Should().Be(value);
        }
    }

    [Fact]
    public void FromDb_rejects_unknown_value()
    {
        var act = () => EnumNaming.FromDb<AttemptStatus>("InProgress");
        act.Should().Throw<InvalidOperationException>();
    }
}

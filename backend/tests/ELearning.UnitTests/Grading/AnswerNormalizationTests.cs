using System.Text;
using ELearning.Domain.Grading;

namespace ELearning.UnitTests.Grading;

/// <summary>docs/08-kiem-thu.md mục 2 — chuẩn hóa văn bản và parse số.</summary>
public class AnswerNormalizationTests
{
    [Fact]
    public void Composed_and_decomposed_vietnamese_are_equivalent()
    {
        const string composed = "Hà Nội";
        var decomposed = composed.Normalize(NormalizationForm.FormD);
        composed.Should().NotBe(decomposed, "hai chuỗi khác nhau về byte");

        AnswerNormalizer.AreEquivalent(composed, decomposed, caseSensitive: false, ignoreAccent: false).Should().BeTrue();
    }

    [Theory]
    [InlineData("  Hà   Nội ", "hà nội")]
    [InlineData("Hà\tNội", "hà nội")]
    [InlineData("Hà Nội", "hà nội")]
    [InlineData("HÀ NỘI", "hà nội")]
    public void Whitespace_and_case_are_normalized(string input, string expected) =>
        AnswerNormalizer.Normalize(input, caseSensitive: false, ignoreAccent: false).Should().Be(expected);

    [Fact]
    public void Case_sensitive_keeps_case()
    {
        AnswerNormalizer.AreEquivalent("Async", "async", caseSensitive: true, ignoreAccent: false).Should().BeFalse();
        AnswerNormalizer.AreEquivalent(" Async ", "Async", caseSensitive: true, ignoreAccent: false).Should().BeTrue();
    }

    [Theory]
    [InlineData("Hà Nội", "ha noi")]
    [InlineData("Đà Nẵng", "da nang")]
    [InlineData("đường", "duong")]
    [InlineData("Nguyễn", "nguyen")]
    public void Ignore_accent_removes_diacritics_including_d_stroke(string input, string plain) =>
        AnswerNormalizer.AreEquivalent(input, plain, caseSensitive: false, ignoreAccent: true).Should().BeTrue();

    [Fact]
    public void Accent_matters_when_not_ignored()
    {
        AnswerNormalizer.AreEquivalent("Hà Nội", "Ha Noi", caseSensitive: false, ignoreAccent: false).Should().BeFalse();
    }

    [Fact]
    public void Clean_content_removes_control_characters_but_keeps_newlines()
    {
        AnswerNormalizer.CleanContent("  Dòng 1\nDòng\u0007 2\t!  ").Should().Be("Dòng 1\nDòng 2\t!");
    }

    [Theory]
    [InlineData("3,5", 3.5)]
    [InlineData("3.5", 3.5)]
    [InlineData(" 42 ", 42)]
    [InlineData("+7", 7)]
    [InlineData("-0,25", -0.25)]
    [InlineData("0", 0)]
    public void Parses_vietnamese_and_invariant_decimals(string input, double expected)
    {
        NumericAnswerParser.TryParse(input, out var value).Should().BeTrue();
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("1,000.5")]
    [InlineData("1.000,5")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("١٢")]
    [InlineData("3,")]
    [InlineData(",5")]
    public void Rejects_ambiguous_or_invalid_numbers(string? input) =>
        NumericAnswerParser.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void Comma_is_never_a_thousands_separator()
    {
        // Bẫy kinh điển: decimal.Parse("3,5", InvariantCulture) = 35 (docs/10-bay-ky-thuat.md mục 3)
        NumericAnswerParser.TryParse("3,5", out var value).Should().BeTrue();
        value.Should().NotBe(35m).And.Be(3.5m);
    }

    [Theory]
    [InlineData(3.0, 3.0, 0.0, true)]
    [InlineData(3.1, 3.0, 0.1, true)]
    [InlineData(3.11, 3.0, 0.1, false)]
    [InlineData(2.9, 3.0, 0.1, true)]
    public void Tolerance_is_inclusive(double actual, double expected, double tolerance, bool within) =>
        NumericAnswerParser.IsWithinTolerance((decimal)actual, (decimal)expected, (decimal)tolerance).Should().Be(within);
}

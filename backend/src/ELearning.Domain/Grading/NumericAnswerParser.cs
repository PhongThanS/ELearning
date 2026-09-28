using System.Globalization;
using System.Text.RegularExpressions;

namespace ELearning.Domain.Grading;

/// <summary>
/// Parse đáp án số kiểu Việt Nam (docs/02-nghiep-vu.md mục 3.3):
/// ',' và '.' đều là dấu thập phân; không có phân cách hàng nghìn, không có mũ.
/// Không dùng decimal.Parse với style mặc định vì "3,5" sẽ thành 35 (docs/10-bay-ky-thuat.md mục 3).
/// </summary>
public static partial class NumericAnswerParser
{
    // [0-9] thay cho \d: \d của .NET khớp cả chữ số Unicode khác (Ả Rập, Devanagari...).
    public const string Pattern = @"^[+-]?[0-9]{1,20}([.,][0-9]{1,10})?$";

    public static bool TryParse(string? input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        if (!NumberRegex().IsMatch(text))
        {
            return false;
        }

        return decimal.TryParse(
            text.Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    public static bool IsWithinTolerance(decimal actual, decimal expected, decimal tolerance) =>
        Math.Abs(actual - expected) <= tolerance;

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NumberRegex();
}

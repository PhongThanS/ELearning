using System.Text.RegularExpressions;

namespace ELearning.Application.Zalo;

/// <summary>Chuẩn hóa số di động Việt Nam sang dạng Zalo yêu cầu: <c>84</c> + 9 chữ số, không dấu +.</summary>
public static partial class ZaloPhone
{
    [GeneratedRegex(@"^84[35789][0-9]{8}$")]
    private static partial Regex MobileRegex();

    /// <summary>"0912 345.678", "+84912345678", "84912345678" → "84912345678"; không phải di động Việt Nam → null.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.StartsWith('0'))
        {
            digits = "84" + digits[1..];
        }

        return MobileRegex().IsMatch(digits) ? digits : null;
    }
}

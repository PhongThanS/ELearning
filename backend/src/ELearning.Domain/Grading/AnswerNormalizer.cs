using System.Globalization;
using System.Text;

namespace ELearning.Domain.Grading;

/// <summary>
/// Chuẩn hóa đáp án dạng văn bản (docs/02-nghiep-vu.md mục 3.1, D-12):
/// NFC → gộp khoảng trắng → (không phân biệt hoa thường) → (bỏ dấu, kể cả đ/Đ).
/// </summary>
public static class AnswerNormalizer
{
    public static string Normalize(string? input, bool caseSensitive, bool ignoreAccent)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // "Hà Nội" dựng sẵn và tổ hợp khác nhau về byte → bắt buộc đưa về NFC trước.
        var text = CollapseWhitespace(input.Normalize(NormalizationForm.FormC));

        if (!caseSensitive)
        {
            text = text.ToLowerInvariant();
        }

        if (ignoreAccent)
        {
            text = RemoveAccents(text);
        }

        return text;
    }

    public static bool AreEquivalent(string? left, string? right, bool caseSensitive, bool ignoreAccent) =>
        string.Equals(Normalize(left, caseSensitive, ignoreAccent), Normalize(right, caseSensitive, ignoreAccent), StringComparison.Ordinal);

    /// <summary>Làm sạch nội dung người dùng nhập: NFC và bỏ ký tự điều khiển (trừ \n, \r, \t).</summary>
    public static string CleanContent(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var normalized = input.Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (!char.IsControl(c) || c is '\n' or '\r' or '\t')
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Trim();
    }

    private static string CollapseWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string RemoveAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // đ/Đ không tách được bằng NFD (docs/10-bay-ky-thuat.md mục 4)
            sb.Append(c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => c,
            });
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

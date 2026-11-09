using ELearning.Domain.Enums;

namespace ELearning.Application.Questions;

/// <summary>
/// Đọc mức độ (độ khó) ghi trong file nhập đề / file Excel: số 1–4, "MĐ1"…"MĐ4", "Mức 1"…"Mức 4", "LEVEL1"…,
/// nhãn NB / TH / VD / VDC, và giá trị cũ EASY / MEDIUM / HARD, Dễ / Trung bình / Khó (= mức 1 / 2 / 3).
/// </summary>
public static class DifficultyLevels
{
    public const string Hint = "Mức độ ghi số 1, 2, 3 hoặc 4 (Mức 1 … Mức 4).";

    public static bool TryParse(string? text, out QuestionDifficulty? difficulty)
    {
        difficulty = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var key = text.Trim().Trim('[', ']', '(', ')').ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (var prefix in new[] { "MỨCĐỘ", "MỨC", "MĐ", "MD", "LEVEL" })
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length && char.IsDigit(key[prefix.Length]))
            {
                key = key[prefix.Length..];
                break;
            }
        }

        difficulty = key switch
        {
            "1" or "NB" or "NHẬNBIẾT" or "EASY" or "DỄ" => QuestionDifficulty.Level1,
            "2" or "TH" or "THÔNGHIỂU" or "MEDIUM" or "TRUNGBÌNH" or "VỪA" => QuestionDifficulty.Level2,
            "3" or "VD" or "VẬNDỤNG" or "HARD" or "KHÓ" => QuestionDifficulty.Level3,
            "4" or "VDC" or "VẬNDỤNGCAO" => QuestionDifficulty.Level4,
            _ => null,
        };
        return difficulty is not null;
    }
}

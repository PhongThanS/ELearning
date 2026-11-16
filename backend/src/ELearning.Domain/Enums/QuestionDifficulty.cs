namespace ELearning.Domain.Enums;

/// <summary>
/// Độ khó (mức độ) của câu hỏi, 5 mức: Mức 1 (nhận biết) … Mức 4 (vận dụng cao), Mức 5 (nâng cao / chuyên sâu).
/// Lưu / trả về "LEVEL1" … "LEVEL5"; dùng để lọc ngân hàng và lập quy tắc pool ngẫu nhiên.
/// </summary>
public enum QuestionDifficulty
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5,
}

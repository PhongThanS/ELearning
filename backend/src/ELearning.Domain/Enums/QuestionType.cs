namespace ELearning.Domain.Enums;

/// <summary>Loại câu hỏi (docs/02-nghiep-vu.md mục 1).</summary>
public enum QuestionType
{
    SingleChoice = 1,
    MultipleChoice = 2,
    TrueFalse = 3,
    FillIn = 4,

    /// <summary>Tự luận: không chấm tự động, giáo viên / admin chấm tay (docs/02-nghiep-vu.md mục 2.3).</summary>
    Essay = 5,
}

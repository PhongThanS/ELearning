namespace ELearning.Domain.Enums;

/// <summary>Khi nào học viên xem lại bài và đáp án (D-09).</summary>
public enum ReviewPolicy
{
    Never = 1,
    AfterSubmit = 2,
    AfterExamEnd = 3,
    AfterLastAttempt = 4,
}

using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Exams;

/// <summary>Tiêu chí chọn câu ứng viên từ ngân hàng; tiêu chí để trống thì không lọc theo tiêu chí đó.</summary>
public sealed record PoolRuleCriteria(Guid? CategoryId, QuestionDifficulty? Difficulty, string? Tag, QuestionType? QuestionType);

/// <summary>
/// Quy tắc pool ngẫu nhiên của một version (docs/02-nghiep-vu.md mục 4.8): mỗi lượt thi bốc ngẫu nhiên
/// <see cref="DrawCount"/> câu trong các câu ứng viên đã snapshot (ExamQuestions có PoolRuleId = Id).
/// </summary>
public sealed class ExamPoolRule : Entity
{
    /// <summary>Số câu ứng viên tối đa được snapshot cho một quy tắc.</summary>
    public const int MaxCandidates = 500;

    private ExamPoolRule()
    {
    }

    internal ExamPoolRule(Guid versionId, int ruleOrder, PoolRuleCriteria criteria, int drawCount, decimal scorePerQuestion)
    {
        // Id sinh ngay để câu ứng viên tham chiếu được trước khi EF theo dõi entity
        Id = Guid.CreateVersion7();
        ExamVersionId = versionId;
        RuleOrder = ruleOrder;
        CategoryId = criteria.CategoryId;
        Difficulty = criteria.Difficulty;
        Tag = string.IsNullOrWhiteSpace(criteria.Tag) ? null : Questions.Question.NormalizeTag(criteria.Tag);
        QuestionType = criteria.QuestionType;

        if (drawCount is < 1 or > ExamVersion.MaxQuestions)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, $"Số câu bốc phải từ 1 đến {ExamVersion.MaxQuestions}.");
        }

        if (scorePerQuestion is < 0.25m or > 100m || scorePerQuestion * 4 % 1 != 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, "Điểm mỗi câu phải từ 0,25 đến 100 và là bội số của 0,25.");
        }

        DrawCount = drawCount;
        ScorePerQuestion = scorePerQuestion;
    }

    public Guid ExamVersionId { get; private set; }

    public int RuleOrder { get; private set; }

    public Guid? CategoryId { get; private set; }

    public QuestionDifficulty? Difficulty { get; private set; }

    public string? Tag { get; private set; }

    public QuestionType? QuestionType { get; private set; }

    public int DrawCount { get; private set; }

    public decimal ScorePerQuestion { get; private set; }

    public PoolRuleCriteria Criteria => new(CategoryId, Difficulty, Tag, QuestionType);

    internal ExamPoolRule CopyTo(Guid versionId) => new(versionId, RuleOrder, Criteria, DrawCount, ScorePerQuestion);
}

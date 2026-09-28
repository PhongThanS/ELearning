using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Questions;

/// <summary>Câu hỏi trong ngân hàng. Hành vi tạo/sửa được bổ sung ở milestone M3.</summary>
public sealed class Question : Entity, IHasRowVersion
{
    private readonly List<QuestionOption> _options = [];
    private readonly List<QuestionAcceptedAnswer> _acceptedAnswers = [];

    private Question()
    {
    }

    public Guid? CategoryId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public ContentFormat ContentFormat { get; private set; }

    public QuestionType QuestionType { get; private set; }

    public AnswerDataType? AnswerDataType { get; private set; }

    public decimal? CorrectAnswerNumber { get; private set; }

    public decimal? NumericTolerance { get; private set; }

    public bool CaseSensitive { get; private set; }

    public bool IgnoreAccent { get; private set; }

    public string? Explanation { get; private set; }

    public decimal DefaultScore { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<QuestionOption> Options => _options;

    public IReadOnlyCollection<QuestionAcceptedAnswer> AcceptedAnswers => _acceptedAnswers;
}

public sealed class QuestionOption : Entity
{
    private QuestionOption()
    {
    }

    public Guid QuestionId { get; private set; }

    public string OptionCode { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public bool IsCorrect { get; private set; }

    public int DisplayOrder { get; private set; }
}

/// <summary>Một đáp án chấp nhận của câu FILL_IN + TEXT (D-12).</summary>
public sealed class QuestionAcceptedAnswer : Entity
{
    private QuestionAcceptedAnswer()
    {
    }

    public Guid QuestionId { get; private set; }

    public string AnswerText { get; private set; } = null!;

    public int DisplayOrder { get; private set; }
}

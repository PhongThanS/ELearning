using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Grading;

namespace ELearning.Domain.Questions;

/// <summary>Dữ liệu đầu vào của một câu hỏi (đã qua validation ở tầng Application).</summary>
public sealed record QuestionData(
    Guid? CategoryId,
    string Content,
    ContentFormat ContentFormat,
    QuestionType QuestionType,
    AnswerDataType? AnswerDataType,
    decimal? CorrectAnswerNumber,
    decimal? NumericTolerance,
    bool CaseSensitive,
    bool IgnoreAccent,
    string? Explanation,
    decimal DefaultScore,
    IReadOnlyList<OptionData> Options,
    IReadOnlyList<string> AcceptedAnswers);

public sealed record OptionData(string OptionCode, string Content, bool IsCorrect);

/// <summary>Câu hỏi trong ngân hàng (docs/02-nghiep-vu.md mục 1).</summary>
public sealed class Question : Entity, IHasRowVersion
{
    public const int MinOptions = 2;
    public const int MaxOptions = 10;
    public const int MaxAcceptedAnswers = 20;
    public const string TrueCode = "TRUE";
    public const string FalseCode = "FALSE";

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

    public static Question Create(string code, QuestionData data, Guid createdBy, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var question = new Question
        {
            Code = code.Trim(),
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = now,
        };
        question.Apply(data);
        return question;
    }

    public void Update(QuestionData data, Guid updatedBy, DateTime now)
    {
        Apply(data);
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SetActive(bool isActive, Guid updatedBy, DateTime now)
    {
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public Question Clone(string newCode, Guid createdBy, DateTime now) => Create(newCode, ToData(), createdBy, now);

    public QuestionData ToData() => new(
        CategoryId,
        Content,
        ContentFormat,
        QuestionType,
        AnswerDataType,
        CorrectAnswerNumber,
        NumericTolerance,
        CaseSensitive,
        IgnoreAccent,
        Explanation,
        DefaultScore,
        _options.OrderBy(o => o.DisplayOrder).Select(o => new OptionData(o.OptionCode, o.Content, o.IsCorrect)).ToList(),
        _acceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList());

    /// <summary>
    /// Kiểm tra bất biến của một câu hỏi (lớp phòng thủ thứ hai sau FluentValidation).
    /// Dùng chung cho snapshot ExamQuestion khi publish.
    /// </summary>
    public static IReadOnlyList<string> Validate(QuestionData data)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(data.Content))
        {
            errors.Add("Nội dung câu hỏi không được để trống.");
        }

        if (data.DefaultScore <= 0)
        {
            errors.Add("Điểm phải lớn hơn 0.");
        }

        var correctCount = data.Options.Count(o => o.IsCorrect);
        var codes = data.Options.Select(o => o.OptionCode).ToList();
        switch (data.QuestionType)
        {
            case QuestionType.SingleChoice or QuestionType.MultipleChoice:
                if (data.Options.Count is < MinOptions or > MaxOptions)
                {
                    errors.Add($"Câu trắc nghiệm phải có từ {MinOptions} đến {MaxOptions} lựa chọn.");
                }

                if (data.QuestionType == QuestionType.SingleChoice && correctCount != 1)
                {
                    errors.Add("Câu chọn một phải có đúng 1 đáp án đúng.");
                }

                if (data.QuestionType == QuestionType.MultipleChoice && correctCount < 1)
                {
                    errors.Add("Câu chọn nhiều phải có ít nhất 1 đáp án đúng.");
                }

                break;
            case QuestionType.TrueFalse:
                if (codes.Count != 2 || !codes.Contains(TrueCode) || !codes.Contains(FalseCode) || correctCount != 1)
                {
                    errors.Add("Câu Đúng/Sai phải có đúng 2 lựa chọn TRUE, FALSE và 1 đáp án đúng.");
                }

                break;
            case QuestionType.FillIn:
                if (data.Options.Count > 0)
                {
                    errors.Add("Câu điền không có lựa chọn.");
                }

                if (data.AnswerDataType == Enums.AnswerDataType.Text
                    && (data.AcceptedAnswers.Count is < 1 or > MaxAcceptedAnswers
                        || data.AcceptedAnswers.Any(a => AnswerNormalizer.Normalize(a, true, false).Length == 0)))
                {
                    errors.Add($"Câu điền chữ phải có từ 1 đến {MaxAcceptedAnswers} đáp án chấp nhận.");
                }

                if (data.AnswerDataType == Enums.AnswerDataType.Number
                    && (data.CorrectAnswerNumber is null || data.NumericTolerance is null or < 0))
                {
                    errors.Add("Câu điền số phải có đáp án và sai số không âm.");
                }

                if (data.AnswerDataType is null)
                {
                    errors.Add("Câu điền phải chọn kiểu đáp án TEXT hoặc NUMBER.");
                }

                break;
        }

        if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
        {
            errors.Add("Mã lựa chọn bị trùng.");
        }

        return errors;
    }

    private void Apply(QuestionData data)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidQuestion, string.Join(" ", errors));
        }

        var isFillIn = data.QuestionType == QuestionType.FillIn;
        var isNumber = isFillIn && data.AnswerDataType == Enums.AnswerDataType.Number;
        var isText = isFillIn && data.AnswerDataType == Enums.AnswerDataType.Text;

        CategoryId = data.CategoryId;
        Content = AnswerNormalizer.CleanContent(data.Content);
        ContentFormat = data.ContentFormat;
        QuestionType = data.QuestionType;
        AnswerDataType = isFillIn ? data.AnswerDataType : null;
        CorrectAnswerNumber = isNumber ? data.CorrectAnswerNumber : null;
        NumericTolerance = isNumber ? data.NumericTolerance : null;
        CaseSensitive = isText && data.CaseSensitive;
        IgnoreAccent = isText && data.IgnoreAccent;
        Explanation = string.IsNullOrWhiteSpace(data.Explanation) ? null : AnswerNormalizer.CleanContent(data.Explanation);
        DefaultScore = data.DefaultScore;

        _options.Clear();
        if (!isFillIn)
        {
            for (var i = 0; i < data.Options.Count; i++)
            {
                var option = data.Options[i];
                _options.Add(new QuestionOption(option.OptionCode, AnswerNormalizer.CleanContent(option.Content), option.IsCorrect, i + 1));
            }
        }

        _acceptedAnswers.Clear();
        if (isText)
        {
            for (var i = 0; i < data.AcceptedAnswers.Count; i++)
            {
                _acceptedAnswers.Add(new QuestionAcceptedAnswer(AnswerNormalizer.CleanContent(data.AcceptedAnswers[i]), i + 1));
            }
        }
    }
}

public sealed class QuestionOption : Entity
{
    private QuestionOption()
    {
    }

    internal QuestionOption(string optionCode, string content, bool isCorrect, int displayOrder)
    {
        OptionCode = optionCode;
        Content = content;
        IsCorrect = isCorrect;
        DisplayOrder = displayOrder;
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

    internal QuestionAcceptedAnswer(string answerText, int displayOrder)
    {
        AnswerText = answerText;
        DisplayOrder = displayOrder;
    }

    public Guid QuestionId { get; private set; }

    public string AnswerText { get; private set; } = null!;

    public int DisplayOrder { get; private set; }
}

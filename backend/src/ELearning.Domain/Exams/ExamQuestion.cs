using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Questions;

namespace ELearning.Domain.Exams;

/// <summary>
/// Snapshot câu hỏi trong một version (D-01, D-02). Là nguồn duy nhất để chấm điểm;
/// không bao giờ chấm từ bảng Questions.
/// </summary>
public sealed class ExamQuestion : Entity
{
    private readonly List<ExamQuestionOption> _options = [];
    private readonly List<ExamQuestionAcceptedAnswer> _acceptedAnswers = [];

    private ExamQuestion()
    {
    }

    public Guid ExamVersionId { get; private set; }

    /// <summary>Chỉ để tham chiếu / đồng bộ, không dùng khi chấm.</summary>
    public Guid? SourceQuestionId { get; private set; }

    public byte[]? SourceRowVersion { get; private set; }

    public DateTime CopiedAt { get; private set; }

    public int QuestionOrder { get; private set; }

    public string Content { get; private set; } = null!;

    public ContentFormat ContentFormat { get; private set; }

    public QuestionType QuestionType { get; private set; }

    public AnswerDataType? AnswerDataType { get; private set; }

    public decimal? CorrectAnswerNumber { get; private set; }

    public decimal? NumericTolerance { get; private set; }

    public bool CaseSensitive { get; private set; }

    public bool IgnoreAccent { get; private set; }

    public string? Explanation { get; private set; }

    public decimal Score { get; private set; }

    public bool IsVoided { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedBy { get; private set; }

    public IReadOnlyCollection<ExamQuestionOption> Options => _options;

    public IReadOnlyCollection<ExamQuestionAcceptedAnswer> AcceptedAnswers => _acceptedAnswers;

    internal static ExamQuestion Snapshot(Guid versionId, Question source, int order, decimal score, DateTime now)
    {
        var question = new ExamQuestion { ExamVersionId = versionId, QuestionOrder = order };
        question.SetScore(score);
        question.RefreshFrom(source, now);
        return question;
    }

    /// <summary>Dữ liệu dạng QuestionData để dùng chung quy tắc kiểm tra với ngân hàng câu hỏi.</summary>
    public QuestionData ToData() => new(
        null,
        Content,
        ContentFormat,
        QuestionType,
        AnswerDataType,
        CorrectAnswerNumber,
        NumericTolerance,
        CaseSensitive,
        IgnoreAccent,
        Explanation,
        Score,
        _options.OrderBy(o => o.DisplayOrder).Select(o => new OptionData(o.OptionCode, o.Content, o.IsCorrect)).ToList(),
        _acceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList());

    internal ExamQuestion CopyTo(Guid versionId, int order, DateTime now)
    {
        var copy = new ExamQuestion
        {
            ExamVersionId = versionId,
            SourceQuestionId = SourceQuestionId,
            SourceRowVersion = SourceRowVersion,
            CopiedAt = now,
            QuestionOrder = order,
            Score = Score,
        };
        copy.ApplyContent(ToData());
        return copy;
    }

    internal void SetScore(decimal score)
    {
        if (score is < 0.25m or > 100m || score * 4 % 1 != 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, "Điểm phải từ 0,25 đến 100 và là bội số của 0,25.");
        }

        Score = score;
    }

    internal void SetOrder(int order) => QuestionOrder = order;

    internal void RefreshFrom(Question source, DateTime now)
    {
        SourceQuestionId = source.Id;
        SourceRowVersion = source.RowVersion;
        CopiedAt = now;
        ApplyContent(source.ToData());
    }

    private void ApplyContent(QuestionData data)
    {
        Content = data.Content;
        ContentFormat = data.ContentFormat;
        QuestionType = data.QuestionType;
        AnswerDataType = data.AnswerDataType;
        CorrectAnswerNumber = data.CorrectAnswerNumber;
        NumericTolerance = data.NumericTolerance;
        CaseSensitive = data.CaseSensitive;
        IgnoreAccent = data.IgnoreAccent;
        Explanation = data.Explanation;

        _options.Clear();
        for (var i = 0; i < data.Options.Count; i++)
        {
            _options.Add(new ExamQuestionOption(data.Options[i].OptionCode, data.Options[i].Content, data.Options[i].IsCorrect, i + 1));
        }

        _acceptedAnswers.Clear();
        for (var i = 0; i < data.AcceptedAnswers.Count; i++)
        {
            _acceptedAnswers.Add(new ExamQuestionAcceptedAnswer(data.AcceptedAnswers[i], i + 1));
        }
    }
}

public sealed class ExamQuestionOption : Entity
{
    private ExamQuestionOption()
    {
    }

    internal ExamQuestionOption(string optionCode, string content, bool isCorrect, int displayOrder)
    {
        OptionCode = optionCode;
        Content = content;
        IsCorrect = isCorrect;
        DisplayOrder = displayOrder;
    }

    public Guid ExamQuestionId { get; private set; }

    public string OptionCode { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    /// <summary>Không bao giờ trả cho học viên khi đang thi.</summary>
    public bool IsCorrect { get; private set; }

    public int DisplayOrder { get; private set; }
}

public sealed class ExamQuestionAcceptedAnswer : Entity
{
    private ExamQuestionAcceptedAnswer()
    {
    }

    internal ExamQuestionAcceptedAnswer(string answerText, int displayOrder)
    {
        AnswerText = answerText;
        DisplayOrder = displayOrder;
    }

    public Guid ExamQuestionId { get; private set; }

    public string AnswerText { get; private set; } = null!;

    public int DisplayOrder { get; private set; }
}

/// <summary>Lịch sử sửa đáp án / hủy câu trên version đã publish (D-11).</summary>
public sealed class AnswerKeyCorrection : Entity
{
    private AnswerKeyCorrection()
    {
    }

    public Guid ExamQuestionId { get; private set; }

    public AnswerKeyCorrectionType CorrectionType { get; private set; }

    public string OldKeyJson { get; private set; } = null!;

    public string NewKeyJson { get; private set; } = null!;

    public string Reason { get; private set; } = null!;

    public int AffectedAttemptCount { get; private set; }

    public Guid CorrectedBy { get; private set; }

    public DateTime CorrectedAt { get; private set; }
}

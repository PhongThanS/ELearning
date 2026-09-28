using ELearning.Domain.Common;
using ELearning.Domain.Enums;

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
}

public sealed class ExamQuestionOption : Entity
{
    private ExamQuestionOption()
    {
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

using ELearning.Domain.Common;

namespace ELearning.Domain.Results;

/// <summary>Kết quả của một lượt thi — nguồn điểm duy nhất (D-20).</summary>
public sealed class ExamResult : Entity
{
    private readonly List<ExamResultHistory> _history = [];

    private ExamResult()
    {
    }

    public Guid AttemptId { get; private set; }

    public Guid ExamId { get; private set; }

    public Guid ExamVersionId { get; private set; }

    public Guid UserId { get; private set; }

    public int TotalQuestion { get; private set; }

    public int AnsweredCount { get; private set; }

    public int CorrectCount { get; private set; }

    public decimal TotalScore { get; private set; }

    public decimal MaxScore { get; private set; }

    public decimal Percentage { get; private set; }

    public bool? Passed { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public int DurationSeconds { get; private set; }

    public int GradingRevision { get; private set; }

    public DateTime GradedAt { get; private set; }

    public DateTime? RegradedAt { get; private set; }

    public IReadOnlyCollection<ExamResultHistory> History => _history;
}

/// <summary>Điểm trước mỗi lần chấm lại (D-11).</summary>
public sealed class ExamResultHistory
{
    private ExamResultHistory()
    {
    }

    public long Id { get; private set; }

    public Guid ExamResultId { get; private set; }

    public int GradingRevision { get; private set; }

    public int CorrectCount { get; private set; }

    public decimal TotalScore { get; private set; }

    public decimal Percentage { get; private set; }

    public bool? Passed { get; private set; }

    public Guid AnswerKeyCorrectionId { get; private set; }

    public DateTime RecordedAt { get; private set; }
}

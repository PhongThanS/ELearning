using ELearning.Domain.Attempts;
using ELearning.Domain.Common;
using ELearning.Domain.Grading;

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

    public static ExamResult Create(ExamAttempt attempt, AttemptScore score, DateTime now)
    {
        if (!attempt.IsFinished || attempt.SubmittedAt is null)
        {
            throw new DomainException(DomainErrorCodes.AttemptNotInProgress, "Chỉ tạo kết quả cho lượt thi đã nộp.");
        }

        var result = new ExamResult
        {
            AttemptId = attempt.Id,
            ExamId = attempt.ExamId,
            ExamVersionId = attempt.ExamVersionId,
            UserId = attempt.UserId,
            StartedAt = attempt.StartedAt,
            SubmittedAt = attempt.SubmittedAt.Value,
            DurationSeconds = (int)Math.Max(0, (attempt.SubmittedAt.Value - attempt.StartedAt).TotalSeconds),
            GradingRevision = 1,
            GradedAt = now,
        };
        result.ApplyScore(score);
        return result;
    }

    /// <summary>Chấm lại sau khi sửa đáp án (D-11): lưu điểm cũ vào lịch sử rồi cập nhật.</summary>
    public bool Regrade(AttemptScore score, Guid correctionId, DateTime now)
    {
        _history.Add(new ExamResultHistory(Id, GradingRevision, CorrectCount, TotalScore, Percentage, Passed, correctionId, now));
        var changed = score.TotalScore != TotalScore || score.Passed != Passed || score.CorrectCount != CorrectCount;
        ApplyScore(score);
        GradingRevision++;
        RegradedAt = now;
        return changed;
    }

    private void ApplyScore(AttemptScore score)
    {
        if (score.MaxScore <= 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidAttempt, "Tổng điểm tối đa phải lớn hơn 0.");
        }

        TotalQuestion = score.TotalQuestion;
        AnsweredCount = score.AnsweredCount;
        CorrectCount = score.CorrectCount;
        TotalScore = score.TotalScore;
        MaxScore = score.MaxScore;
        Percentage = score.Percentage;
        Passed = score.Passed;
    }
}

/// <summary>Điểm trước mỗi lần chấm lại (D-11).</summary>
public sealed class ExamResultHistory
{
    private ExamResultHistory()
    {
    }

    internal ExamResultHistory(
        Guid examResultId, int gradingRevision, int correctCount, decimal totalScore, decimal percentage, bool? passed,
        Guid answerKeyCorrectionId, DateTime recordedAt)
    {
        ExamResultId = examResultId;
        GradingRevision = gradingRevision;
        CorrectCount = correctCount;
        TotalScore = totalScore;
        Percentage = percentage;
        Passed = passed;
        AnswerKeyCorrectionId = answerKeyCorrectionId;
        RecordedAt = recordedAt;
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

using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Attempts;

/// <summary>Lượt làm bài. Hành vi start/submit/extend được bổ sung ở milestone M5.</summary>
public sealed class ExamAttempt : Entity, IHasRowVersion
{
    private readonly List<AttemptQuestion> _questions = [];

    private ExamAttempt()
    {
    }

    public Guid ExamId { get; private set; }

    public Guid ExamVersionId { get; private set; }

    public Guid UserId { get; private set; }

    public int AttemptNumber { get; private set; }

    public AttemptStatus Status { get; private set; }

    public SubmitReason? SubmitReason { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime ExpiredAt { get; private set; }

    public int TimeExtensionMinutes { get; private set; }

    public DateTime? SubmittedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public Guid? CancelledBy { get; private set; }

    public string? CancelReason { get; private set; }

    public int QuestionCount { get; private set; }

    public string? StartedIp { get; private set; }

    public string? StartedUserAgent { get; private set; }

    public string? SubmittedIp { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<AttemptQuestion> Questions => _questions;
}

/// <summary>Câu hỏi của lượt thi: chỉ tham chiếu snapshot + thứ tự (D-01).</summary>
public sealed class AttemptQuestion : Entity
{
    private AttemptQuestion()
    {
    }

    public Guid AttemptId { get; private set; }

    public Guid ExamQuestionId { get; private set; }

    public int QuestionOrder { get; private set; }

    /// <summary>Ví dụ "C,A,D,B"; null = theo DisplayOrder của snapshot.</summary>
    public string? OptionOrder { get; private set; }

    public AttemptAnswer Answer { get; private set; } = null!;
}

public sealed class AttemptAnswer : Entity
{
    private readonly List<AttemptAnswerOption> _selectedOptions = [];

    private AttemptAnswer()
    {
    }

    public Guid AttemptQuestionId { get; private set; }

    /// <summary>Chuỗi gốc của câu FILL_IN (cả TEXT lẫn NUMBER).</summary>
    public string? AnswerText { get; private set; }

    public decimal? AnswerNumber { get; private set; }

    public bool IsAnswered { get; private set; }

    public bool IsMarkedForReview { get; private set; }

    /// <summary>Số tăng dần do client sinh, chỉ để sắp thứ tự ghi (D-18).</summary>
    public long ClientSeq { get; private set; }

    public DateTime? AnsweredAt { get; private set; }

    public int SaveCount { get; private set; }

    public bool? IsCorrect { get; private set; }

    public decimal? Score { get; private set; }

    public DateTime? GradedAt { get; private set; }

    public IReadOnlyCollection<AttemptAnswerOption> SelectedOptions => _selectedOptions;
}

public sealed class AttemptAnswerOption
{
    private AttemptAnswerOption()
    {
    }

    public AttemptAnswerOption(Guid attemptAnswerId, string optionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);
        AttemptAnswerId = attemptAnswerId;
        OptionCode = optionCode;
    }

    public Guid AttemptAnswerId { get; private set; }

    public string OptionCode { get; private set; } = null!;
}

/// <summary>Sự kiện ghi nhận trong lượt thi (rời trang, mất mạng...) — công cụ răn đe, không chống gian lận tuyệt đối.</summary>
public sealed class AttemptEvent
{
    private AttemptEvent()
    {
    }

    public long Id { get; private set; }

    public Guid AttemptId { get; private set; }

    public AttemptEventType EventType { get; private set; }

    public DateTime? ClientTime { get; private set; }

    public DateTime ServerTime { get; private set; }

    public string? IpAddress { get; private set; }

    public string? Detail { get; private set; }
}

using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Grading;

namespace ELearning.Domain.Attempts;

/// <summary>
/// Lượt làm bài (docs/02-nghiep-vu.md mục 6).
/// IN_PROGRESS → SUBMITTED | AUTO_SUBMITTED | CANCELLED; không có chuyển trạng thái nào đi ra từ trạng thái kết thúc.
/// </summary>
public sealed class ExamAttempt : Entity, IHasRowVersion
{
    public const int MaxExtensionMinutes = 240;

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

    public bool IsInProgress => Status == AttemptStatus.InProgress;

    public bool IsFinished => Status is AttemptStatus.Submitted or AttemptStatus.AutoSubmitted;

    /// <summary>
    /// Bắt đầu lượt thi: ExpiredAt = min(StartedAt + thời lượng, EndAt) (D-05);
    /// tạo sẵn câu hỏi (đánh số lại 1..N) và câu trả lời trống.
    /// </summary>
    /// <param name="random">Nguồn ngẫu nhiên khi phiên bản bật xáo câu / đáp án (test truyền seed cố định).</param>
    public static ExamAttempt Start(
        Exam exam, ExamVersion version, Guid userId, int attemptNumber, DateTime now, string? ip, string? userAgent,
        Random? random = null)
    {
        if (version.Status != ExamVersionStatus.Published || version.ExamId != exam.Id)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Chỉ bắt đầu được trên phiên bản đang publish.");
        }

        var expiredAt = now.AddMinutes(version.DurationMinutes);
        if (exam.EndAt is { } endAt && endAt < expiredAt)
        {
            expiredAt = endAt;
        }

        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            ExamVersionId = version.Id,
            UserId = userId,
            AttemptNumber = attemptNumber,
            Status = AttemptStatus.InProgress,
            StartedAt = now,
            ExpiredAt = expiredAt,
            StartedIp = ip,
            StartedUserAgent = userAgent is { Length: > 500 } ? userAgent[..500] : userAgent,
            CreatedAt = now,
        };

        // Xáo (nếu bật) được chốt vào AttemptQuestions lúc bắt đầu: tải lại trang vẫn giữ nguyên thứ tự
        random ??= Random.Shared;
        var questions = version.Questions.OrderBy(q => q.QuestionOrder).ToArray();
        if (version.ShuffleQuestions)
        {
            random.Shuffle(questions);
        }

        var order = 1;
        foreach (var question in questions)
        {
            attempt._questions.Add(new AttemptQuestion(question.Id, order++, ShuffledOptionOrder(version, question, random)));
        }

        attempt.QuestionCount = attempt._questions.Count;
        return attempt;
    }

    /// <summary>
    /// Thứ tự lựa chọn đã xáo, ví dụ "C,A,D,B". Không xáo câu Đúng/Sai (thứ tự Đúng → Sai là quy ước) và câu điền.
    /// </summary>
    private static string? ShuffledOptionOrder(ExamVersion version, ExamQuestion question, Random random)
    {
        if (!version.ShuffleOptions || question.QuestionType is not (QuestionType.SingleChoice or QuestionType.MultipleChoice))
        {
            return null;
        }

        var codes = question.Options.OrderBy(o => o.DisplayOrder).Select(o => o.OptionCode).ToArray();
        if (codes.Length == 0)
        {
            throw new InvalidOperationException("Phải nạp Options của ExamQuestion trước khi bắt đầu lượt thi có xáo đáp án.");
        }

        random.Shuffle(codes);
        return string.Join(",", codes);
    }

    /// <summary>Còn nhận câu trả lời: đang làm và chưa quá ExpiredAt + ân hạn (D-05).</summary>
    public bool AcceptsAnswers(DateTime now, TimeSpan grace) => IsInProgress && now <= ExpiredAt + grace;

    public bool IsExpired(DateTime now, TimeSpan grace) => IsInProgress && now > ExpiredAt + grace;

    /// <summary>
    /// Kết thúc lượt thi. Học viên nộp sau ân hạn vẫn được ghi là AUTO_SUBMITTED / TIME_EXPIRED (docs/02-nghiep-vu.md mục 6.7).
    /// </summary>
    public void Submit(DateTime now, TimeSpan grace, SubmitReason requestedReason, string? ip)
    {
        EnsureInProgress();
        var reason = requestedReason == Enums.SubmitReason.Student && now > ExpiredAt + grace
            ? Enums.SubmitReason.TimeExpired
            : requestedReason;

        Status = reason == Enums.SubmitReason.Student ? AttemptStatus.Submitted : AttemptStatus.AutoSubmitted;
        SubmitReason = reason;

        // Thời điểm nộp không vượt quá hạn: tự nộp do hết giờ được ghi tại ExpiredAt
        SubmittedAt = reason == Enums.SubmitReason.TimeExpired && now > ExpiredAt ? ExpiredAt : now;
        SubmittedIp = ip;
    }

    public void Extend(int minutes)
    {
        EnsureInProgress();
        if (minutes is < 1 or > MaxExtensionMinutes)
        {
            throw new DomainException(DomainErrorCodes.InvalidAttempt, $"Số phút gia hạn phải từ 1 đến {MaxExtensionMinutes}.");
        }

        ExpiredAt = ExpiredAt.AddMinutes(minutes);
        TimeExtensionMinutes += minutes;
    }

    public void Cancel(Guid cancelledBy, string reason, DateTime now)
    {
        if (Status == AttemptStatus.Cancelled)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Lượt thi đã bị hủy.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = AttemptStatus.Cancelled;
        CancelledAt = now;
        CancelledBy = cancelledBy;
        CancelReason = reason.Trim();
    }

    private void EnsureInProgress()
    {
        if (!IsInProgress)
        {
            throw new DomainException(DomainErrorCodes.AttemptNotInProgress, "Lượt thi đã kết thúc.");
        }
    }
}

/// <summary>Câu hỏi của lượt thi: chỉ tham chiếu snapshot + thứ tự (D-01).</summary>
public sealed class AttemptQuestion : Entity
{
    private AttemptQuestion()
    {
    }

    internal AttemptQuestion(Guid examQuestionId, int questionOrder, string? optionOrder)
    {
        ExamQuestionId = examQuestionId;
        QuestionOrder = questionOrder;
        OptionOrder = optionOrder;
        Answer = new AttemptAnswer();
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

    internal AttemptAnswer()
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

    public StudentAnswer ToStudentAnswer() =>
        new(_selectedOptions.Select(o => o.OptionCode).ToList(), AnswerText, AnswerNumber);

    /// <summary>
    /// Ghi câu trả lời nếu clientSeq mới hơn giá trị đang lưu (D-18). Trả về false nếu bị bỏ qua do cũ hơn.
    /// </summary>
    public bool Apply(
        IReadOnlyCollection<string> selectedOptions, string? answerText, decimal? answerNumber, bool isMarkedForReview, long clientSeq, DateTime now)
    {
        if (clientSeq <= ClientSeq)
        {
            return false;
        }

        _selectedOptions.RemoveAll(o => !selectedOptions.Contains(o.OptionCode));
        foreach (var code in selectedOptions.Where(c => _selectedOptions.TrueForAll(o => o.OptionCode != c)))
        {
            _selectedOptions.Add(new AttemptAnswerOption(Id, code));
        }

        AnswerText = string.IsNullOrWhiteSpace(answerText) ? null : answerText;
        AnswerNumber = AnswerText is null ? null : answerNumber;
        IsAnswered = _selectedOptions.Count > 0 || AnswerText is not null;
        IsMarkedForReview = isMarkedForReview;
        ClientSeq = clientSeq;
        AnsweredAt = now;
        SaveCount++;
        return true;
    }

    public void Grade(GradingResult result, DateTime now)
    {
        IsCorrect = result.IsCorrect;
        Score = result.Score;
        GradedAt = now;
    }
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
    public const int MaxEventsPerAttempt = 1000;

    private AttemptEvent()
    {
    }

    public AttemptEvent(Guid attemptId, AttemptEventType eventType, DateTime? clientTime, DateTime serverTime, string? ipAddress, string? detail)
    {
        AttemptId = attemptId;
        EventType = eventType;
        ClientTime = clientTime;
        ServerTime = serverTime;
        IpAddress = ipAddress;
        Detail = detail is { Length: > 500 } ? detail[..500] : detail;
    }

    public long Id { get; private set; }

    public Guid AttemptId { get; private set; }

    public AttemptEventType EventType { get; private set; }

    public DateTime? ClientTime { get; private set; }

    public DateTime ServerTime { get; private set; }

    public string? IpAddress { get; private set; }

    public string? Detail { get; private set; }
}

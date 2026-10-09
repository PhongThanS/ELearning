using ELearning.Application.Exams;
using ELearning.Domain.Enums;
using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Attempts;

/// <summary>Tình trạng đề với một học viên (docs/05-api.md mục 6.7).</summary>
public enum ExamAvailability
{
    NotStarted = 1,
    Available = 2,
    InProgress = 3,
    NoAttemptsLeft = 4,
    Ended = 5,
    Closed = 6,
}

public sealed record StudentExamListItemDto(
    Guid ExamId,
    string Code,
    string Name,
    DateTime? StartAt,
    DateTime? EndAt,
    int DurationMinutes,
    int QuestionCount,
    int MaxAttempts,
    int UsedAttempts,
    int RemainingAttempts,
    Guid? InProgressAttemptId,
    decimal? OfficialScore,
    ExamAvailability Availability);

public sealed record StudentAttemptSummaryDto(
    Guid AttemptId,
    int AttemptNumber,
    AttemptStatus Status,
    DateTime StartedAt,
    DateTime ExpiredAt,
    DateTime? SubmittedAt,
    bool ScoreVisible,
    decimal? TotalScore,
    decimal? MaxScore,
    decimal? Percentage,
    bool? Passed,
    bool PendingManualGrading = false);

public sealed record StudentExamDetailDto(
    Guid ExamId,
    string Code,
    string Name,
    string? Description,
    string? Instructions,
    DateTime? StartAt,
    DateTime? EndAt,
    int DurationMinutes,
    int QuestionCount,
    decimal MaxScore,
    decimal? PassPercentage,
    int MaxAttempts,
    int UsedAttempts,
    int RemainingAttempts,
    Guid? InProgressAttemptId,
    decimal? OfficialScore,
    ExamAvailability Availability,
    IReadOnlyList<StudentAttemptSummaryDto> Attempts);

public sealed record AttemptAnswerStateDto(
    IReadOnlyList<string> SelectedOptions, string? AnswerText, bool IsMarkedForReview, long ClientSeq);

/// <summary>Câu hỏi trong lượt thi: KHÔNG có đáp án đúng / giải thích (docs/05-api.md mục 6.7).</summary>
public sealed record AttemptQuestionDto(
    Guid Id,
    int Order,
    string Content,
    ContentFormat ContentFormat,
    QuestionType Type,
    AnswerDataType? AnswerDataType,
    decimal Score,
    IReadOnlyList<PlayerOptionDto> Options,
    AttemptAnswerStateDto Answer);

public sealed record AttemptDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamName,
    int AttemptNumber,
    AttemptStatus Status,
    bool Resumed,
    DateTime StartedAt,
    DateTime ExpiredAt,
    DateTime ServerTime,
    IReadOnlyList<AttemptQuestionDto> Questions,
    IReadOnlyDictionary<string, string>? Media = null);

public sealed record SaveAnswerItem(
    Guid QuestionId,
    long ClientSeq,
    IReadOnlyList<string>? SelectedOptions,
    string? AnswerText,
    bool IsMarkedForReview);

public sealed record SaveAnswersRequest(IReadOnlyList<SaveAnswerItem> Answers);

public sealed record SavedAnswerDto(Guid QuestionId, bool Applied, long ClientSeq);

public sealed record SaveAnswersResponse(DateTime ServerTime, DateTime ExpiredAt, IReadOnlyList<SavedAnswerDto> Answers);

public sealed record AttemptEventItem(AttemptEventType Type, DateTime? ClientTime, string? Detail);

public sealed record RecordEventsRequest(IReadOnlyList<AttemptEventItem> Events);

public sealed record RecordEventsResponse(int Accepted);

/// <summary>Xem lại một câu (chỉ khi chính sách cho phép).</summary>
public sealed record ReviewQuestionDto(
    Guid Id,
    int Order,
    string Content,
    ContentFormat ContentFormat,
    QuestionType Type,
    AnswerDataType? AnswerDataType,
    decimal MaxScore,
    IReadOnlyList<PlayerOptionDto> Options,
    IReadOnlyList<string> SelectedOptions,
    string? AnswerText,
    IReadOnlyList<string> CorrectOptions,
    IReadOnlyList<string> AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    bool IsCorrect,
    decimal Score,
    bool IsVoided,
    string? Explanation,
    string? ManualComment = null);

/// <summary>Kết quả cho học viên, đã áp chính sách hiển thị (D-09, docs/05-api.md mục 6.7).</summary>
public sealed record StudentResultDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamName,
    int AttemptNumber,
    AttemptStatus Status,
    SubmitReason? SubmitReason,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    int? DurationSeconds,
    bool ScoreVisible,
    decimal? TotalScore,
    decimal? MaxScore,
    decimal? Percentage,
    int? CorrectCount,
    int TotalQuestion,
    bool? Passed,
    bool ReviewAvailable,
    DateTime? ReviewAvailableAt,
    IReadOnlyList<ReviewQuestionDto>? Questions,
    bool PendingManualGrading = false,
    IReadOnlyDictionary<string, string>? Media = null);

public sealed record StudentHistoryItemDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamCode,
    string ExamName,
    int AttemptNumber,
    AttemptStatus Status,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    bool ScoreVisible,
    decimal? TotalScore,
    decimal? MaxScore,
    decimal? Percentage,
    bool? Passed,
    bool PendingManualGrading = false);

public sealed record StudentExamListQuery : PageRequest
{
    public string? Keyword { get; init; }
    /// <summary>Chỉ đề được gán cho lớp này (học viên phải thuộc lớp).</summary>
    public Guid? ClassroomId { get; init; }
}

public sealed record StudentHistoryQuery : PageRequest;

internal sealed class SaveAnswersRequestValidator : AbstractValidator<SaveAnswersRequest>
{
    /// <summary>Đủ cho bài tự luận; câu điền ngắn hơn nhiều nhưng không cần giới hạn riêng.</summary>
    public const int MaxAnswerTextLength = 20_000;

    public const int MaxItems = 50;

    public SaveAnswersRequestValidator()
    {
        RuleFor(r => r.Answers).NotEmpty().WithErrorCode("ANSWERS_REQUIRED").WithMessage("Không có câu trả lời nào.")
            .Must(a => a.Count <= MaxItems).WithErrorCode("TOO_MANY_ANSWERS").WithMessage($"Tối đa {MaxItems} câu mỗi lần lưu.")
            .Must(a => a.Select(x => x.QuestionId).Distinct().Count() == a.Count)
            .WithErrorCode("DUPLICATE_QUESTION").WithMessage("Mỗi câu chỉ được gửi một lần trong một lô.");
        RuleForEach(r => r.Answers).ChildRules(item =>
        {
            item.RuleFor(i => i.ClientSeq).GreaterThan(0).WithErrorCode("CLIENT_SEQ_INVALID").WithMessage("clientSeq phải lớn hơn 0.");
            item.RuleFor(i => i.AnswerText).MaximumLength(MaxAnswerTextLength).WithErrorCode("ANSWER_TOO_LONG")
                .WithMessage($"Câu trả lời tối đa {MaxAnswerTextLength} ký tự.");
            item.RuleFor(i => i.SelectedOptions).Must(o => o is null || o.Count <= 10)
                .WithErrorCode("TOO_MANY_OPTIONS").WithMessage("Chọn quá nhiều lựa chọn.");
        });
    }
}

internal sealed class RecordEventsRequestValidator : AbstractValidator<RecordEventsRequest>
{
    public RecordEventsRequestValidator()
    {
        RuleFor(r => r.Events).NotNull().Must(e => e.Count is > 0 and <= 50)
            .WithErrorCode("EVENTS_COUNT_INVALID").WithMessage("Mỗi lần gửi từ 1 đến 50 sự kiện.");
        RuleForEach(r => r.Events).ChildRules(e =>
        {
            e.RuleFor(x => x.Type).IsInEnum();
            e.RuleFor(x => x.Detail).MaximumLength(500);
        });
    }
}

using ELearning.Domain.Enums;
using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Admin;

public sealed record AdminAttemptListQuery : PageRequest
{
    public AttemptStatus? Status { get; init; }

    public Guid? UserId { get; init; }

    public string? Keyword { get; init; }
}

public sealed record AdminAttemptListItemDto(
    Guid AttemptId,
    Guid UserId,
    string UserName,
    string FullName,
    int AttemptNumber,
    int VersionNumber,
    AttemptStatus Status,
    SubmitReason? SubmitReason,
    DateTime StartedAt,
    DateTime ExpiredAt,
    DateTime? SubmittedAt,
    int TimeExtensionMinutes,
    decimal? TotalScore,
    decimal? MaxScore,
    decimal? Percentage,
    bool? Passed,
    int EventCount);

public sealed record AdminAnswerDto(
    Guid AttemptQuestionId,
    int Order,
    string Content,
    QuestionType Type,
    AnswerDataType? AnswerDataType,
    decimal MaxScore,
    IReadOnlyList<string> SelectedOptions,
    string? AnswerText,
    bool IsAnswered,
    bool IsMarkedForReview,
    DateTime? AnsweredAt,
    int SaveCount,
    IReadOnlyList<string> CorrectOptions,
    IReadOnlyList<string> AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    bool? IsCorrect,
    decimal? Score,
    bool IsVoided);

public sealed record AdminEventDto(AttemptEventType Type, DateTime? ClientTime, DateTime ServerTime, string? IpAddress, string? Detail);

public sealed record AdminAttemptDetailDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamCode,
    string ExamName,
    Guid ExamVersionId,
    int VersionNumber,
    Guid UserId,
    string UserName,
    string FullName,
    int AttemptNumber,
    AttemptStatus Status,
    SubmitReason? SubmitReason,
    DateTime StartedAt,
    DateTime ExpiredAt,
    DateTime? SubmittedAt,
    int TimeExtensionMinutes,
    DateTime? CancelledAt,
    string? CancelReason,
    string? StartedIp,
    string? StartedUserAgent,
    string? SubmittedIp,
    decimal? TotalScore,
    decimal? MaxScore,
    decimal? Percentage,
    int? CorrectCount,
    bool? Passed,
    int? GradingRevision,
    IReadOnlyList<AdminAnswerDto> Answers,
    IReadOnlyList<AdminEventDto> Events);

public sealed record ExtendAttemptRequest(int Minutes, string Reason);

public sealed record AdminReasonRequest(string Reason);

public sealed record AdminResultQuery : PageRequest
{
    /// <summary>true: chỉ điểm chính thức của mỗi học viên theo RetakeScoringPolicy (D-08).</summary>
    public bool Official { get; init; }

    public string? Keyword { get; init; }
}

public sealed record AdminResultRowDto(
    Guid AttemptId,
    Guid UserId,
    string UserName,
    string FullName,
    string Email,
    int AttemptNumber,
    AttemptStatus Status,
    DateTime StartedAt,
    DateTime SubmittedAt,
    int DurationSeconds,
    decimal TotalScore,
    decimal MaxScore,
    decimal Percentage,
    int CorrectCount,
    int TotalQuestion,
    bool? Passed,
    bool IsOfficial,
    int PendingManualCount = 0);

public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

public sealed record CorrectAnswerKeyRequest(
    IReadOnlyList<string>? CorrectOptionCodes,
    IReadOnlyList<string>? AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    decimal? NumericTolerance,
    string Reason);

public sealed record RegradeSummaryDto(Guid CorrectionId, int AffectedAttempts, int ChangedResults);

public sealed record AnswerKeyCorrectionDto(
    Guid Id,
    Guid ExamQuestionId,
    int VersionNumber,
    int QuestionOrder,
    AnswerKeyCorrectionType CorrectionType,
    string OldKeyJson,
    string NewKeyJson,
    string Reason,
    int AffectedAttemptCount,
    Guid CorrectedBy,
    string CorrectedByName,
    DateTime CorrectedAt);

public sealed record DashboardDto(
    int TotalUsers,
    int TotalStudents,
    int TotalExams,
    int OpenExams,
    int AttemptsToday,
    int InProgressAttempts,
    decimal? AveragePercentage30Days,
    decimal? PassRate30Days,
    IReadOnlyList<UpcomingExamDto> UpcomingExams);

public sealed record UpcomingExamDto(Guid ExamId, string Code, string Name, DateTime? StartAt, DateTime? EndAt, int InProgressAttempts);

public sealed record OptionStatDto(string OptionCode, int SelectedCount);

public sealed record QuestionStatDto(
    Guid ExamQuestionId,
    int Order,
    string ContentPreview,
    QuestionType Type,
    int AttemptCount,
    int AnsweredCount,
    int CorrectCount,
    int WrongCount,
    int BlankCount,
    decimal? CorrectRate,
    decimal? AverageScore,
    decimal MaxScore,
    bool IsVoided,
    IReadOnlyList<OptionStatDto> OptionDistribution);

public sealed record AuditLogQuery : PageRequest
{
    public Guid? UserId { get; init; }

    public string? Action { get; init; }

    public string? EntityName { get; init; }

    public Guid? EntityId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed record AuditLogDto(
    long Id,
    DateTime CreatedAt,
    Guid? UserId,
    string? UserName,
    string Action,
    string? EntityName,
    Guid? EntityId,
    string? OldValue,
    string? NewValue,
    string? Reason,
    string? IpAddress,
    string? TraceId);

/// <summary>Truy vấn báo cáo nặng bằng Dapper (docs/03-kien-truc.md mục 3.4).</summary>
public interface IReportQuery
{
    Task<DashboardDto> GetDashboardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime nowUtc, CancellationToken ct);

    Task<IReadOnlyList<QuestionStatDto>> GetQuestionStatisticsAsync(Guid versionId, CancellationToken ct);
}

/// <summary>Xuất kết quả ra Excel (ClosedXML).</summary>
public interface IResultExporter
{
    byte[] Export(string examCode, string examName, IReadOnlyList<AdminResultRowDto> rows);
}

internal sealed class ExtendAttemptRequestValidator : AbstractValidator<ExtendAttemptRequest>
{
    public ExtendAttemptRequestValidator()
    {
        RuleFor(r => r.Minutes).InclusiveBetween(1, Domain.Attempts.ExamAttempt.MaxExtensionMinutes)
            .WithErrorCode("MINUTES_INVALID").WithMessage($"Số phút gia hạn phải từ 1 đến {Domain.Attempts.ExamAttempt.MaxExtensionMinutes}.");
        RuleFor(r => r.Reason).NotEmpty().WithErrorCode("REASON_REQUIRED").WithMessage("Vui lòng nhập lý do.").MaximumLength(500);
    }
}

internal sealed class AdminReasonRequestValidator : AbstractValidator<AdminReasonRequest>
{
    public AdminReasonRequestValidator() =>
        RuleFor(r => r.Reason).NotEmpty().WithErrorCode("REASON_REQUIRED").WithMessage("Vui lòng nhập lý do.").MaximumLength(500);
}

internal sealed class CorrectAnswerKeyRequestValidator : AbstractValidator<CorrectAnswerKeyRequest>
{
    public CorrectAnswerKeyRequestValidator()
    {
        RuleFor(r => r.Reason).NotEmpty().WithErrorCode("REASON_REQUIRED").WithMessage("Vui lòng nhập lý do sửa đáp án.")
            .MaximumLength(1000);
        RuleFor(r => r).Must(r => r.CorrectOptionCodes is not null || r.AcceptedAnswers is not null
                || r.CorrectAnswerNumber is not null || r.NumericTolerance is not null)
            .WithErrorCode("ANSWER_KEY_REQUIRED").WithMessage("Chưa nhập đáp án mới.");
        RuleFor(r => r.NumericTolerance).GreaterThanOrEqualTo(0).When(r => r.NumericTolerance is not null)
            .WithErrorCode("TOLERANCE_NEGATIVE").WithMessage("Sai số không được âm.");
    }
}

using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Exams;

public sealed record ExamListQuery : PageRequest
{
    public ExamStatus? Status { get; init; }

    public string? Keyword { get; init; }
}

public sealed record ExamListItemDto(
    Guid Id,
    string Code,
    string Name,
    ExamStatus Status,
    DateTime? StartAt,
    DateTime? EndAt,
    int MaxAttempts,
    AccessMode AccessMode,
    int? PublishedVersionNumber,
    bool HasDraftVersion,
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record VersionSummaryDto(
    Guid Id,
    int VersionNumber,
    ExamVersionStatus Status,
    int QuestionCount,
    decimal MaxScore,
    DateTime? PublishedAt,
    DateTime? ArchivedAt,
    DateTime CreatedAt);

public sealed record ExamDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Instructions,
    ExamStatus Status,
    DateTime? StartAt,
    DateTime? EndAt,
    int MaxAttempts,
    AccessMode AccessMode,
    RetakeScoringPolicy RetakeScoringPolicy,
    Guid? PublishedVersionId,
    Guid? DraftVersionId,
    int AssignmentCount,
    bool HasAttempts,
    IReadOnlyList<VersionSummaryDto> Versions,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ClosedAt,
    string RowVersion);

public sealed record VersionOptionDto(string OptionCode, string Content, bool IsCorrect, int DisplayOrder);

/// <summary>Câu hỏi trong version cho admin (đầy đủ đáp án).</summary>
public sealed record VersionQuestionDto(
    Guid Id,
    int Order,
    Guid? SourceQuestionId,
    string? SourceCode,
    bool SourceChanged,
    string Content,
    ContentFormat ContentFormat,
    QuestionType QuestionType,
    AnswerDataType? AnswerDataType,
    decimal Score,
    bool IsVoided,
    IReadOnlyList<VersionOptionDto> Options,
    IReadOnlyList<string> AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    decimal? NumericTolerance,
    bool CaseSensitive,
    bool IgnoreAccent,
    string? Explanation);

public sealed record VersionDetailDto(
    Guid Id,
    Guid ExamId,
    int VersionNumber,
    ExamVersionStatus Status,
    int DurationMinutes,
    decimal? PassPercentage,
    ScoreVisibility ScoreVisibility,
    ReviewPolicy ReviewPolicy,
    bool ShuffleQuestions,
    bool ShuffleOptions,
    int QuestionCount,
    decimal MaxScore,
    DateTime? PublishedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<VersionQuestionDto> Questions,
    string RowVersion);

public sealed record PublishValidationDto(bool IsValid, IReadOnlyList<PublishIssue> Issues);

/// <summary>Option hiển thị cho học viên: không có IsCorrect (docs/07-bao-mat.md mục 6).</summary>
public sealed record PlayerOptionDto(string Code, string Content);

/// <summary>Câu hỏi hiển thị cho học viên / preview: không có đáp án, giải thích (docs/05-api.md mục 6.7).</summary>
public sealed record PlayerQuestionDto(
    Guid Id,
    int Order,
    string Content,
    ContentFormat ContentFormat,
    QuestionType Type,
    AnswerDataType? AnswerDataType,
    decimal Score,
    IReadOnlyList<PlayerOptionDto> Options);

public sealed record ExamPreviewDto(
    Guid ExamId,
    Guid VersionId,
    string ExamName,
    string? Instructions,
    int DurationMinutes,
    int QuestionCount,
    decimal MaxScore,
    IReadOnlyList<PlayerQuestionDto> Questions);

public sealed record AssignedGroupDto(Guid Id, string Code, string Name, int MemberCount);

public sealed record AssignedUserDto(Guid Id, string UserName, string FullName);

public sealed record AssignmentsDto(AccessMode AccessMode, IReadOnlyList<AssignedGroupDto> Groups, IReadOnlyList<AssignedUserDto> Users);

public sealed record UserOverrideDto(Guid ExamId, Guid UserId, int ExtraAttempts, string? Note, DateTime UpdatedAt);

public sealed record UserOverrideListItemDto(
    Guid UserId, string UserName, string FullName, int ExtraAttempts, string? Note, DateTime UpdatedAt);

public record ExamDetailsInput
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Instructions { get; init; }

    public DateTime? StartAt { get; init; }

    public DateTime? EndAt { get; init; }

    public int MaxAttempts { get; init; } = 1;

    public AccessMode AccessMode { get; init; } = AccessMode.Assigned;

    public RetakeScoringPolicy RetakeScoringPolicy { get; init; } = RetakeScoringPolicy.Highest;

    internal ExamDetails ToDetails() =>
        new(Name, Description, Instructions, ToUtc(StartAt), ToUtc(EndAt), MaxAttempts, AccessMode, RetakeScoringPolicy);

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc),
    };
}

public sealed record CreateExamRequest : ExamDetailsInput
{
    public string Code { get; init; } = string.Empty;

    public int DurationMinutes { get; init; } = 60;

    public decimal? PassPercentage { get; init; }

    public ScoreVisibility ScoreVisibility { get; init; } = ScoreVisibility.Immediate;

    public ReviewPolicy ReviewPolicy { get; init; } = ReviewPolicy.Never;

    public bool ShuffleQuestions { get; init; }

    public bool ShuffleOptions { get; init; }
}

public sealed record UpdateExamRequest : ExamDetailsInput
{
    /// <summary>Chỉ đổi được khi đề chưa từng publish.</summary>
    public string? Code { get; init; }

    public string RowVersion { get; init; } = string.Empty;
}

public sealed record UpdateVersionRequest(
    int DurationMinutes,
    decimal? PassPercentage,
    ScoreVisibility ScoreVisibility,
    ReviewPolicy ReviewPolicy,
    string RowVersion,
    bool ShuffleQuestions = false,
    bool ShuffleOptions = false);

public sealed record CreateVersionRequest(Guid? CopyFromVersionId);

public sealed record AddVersionQuestionsRequest(IReadOnlyList<Guid> QuestionIds, decimal? Score);

public sealed record UpdateVersionQuestionRequest(decimal Score);

public sealed record ReorderQuestionsRequest(IReadOnlyList<Guid> ExamQuestionIds);

public sealed record SyncQuestionsRequest(IReadOnlyList<Guid>? ExamQuestionIds);

public sealed record CloseExamRequest(bool ForceSubmitInProgress);

public sealed record CloneExamRequest(string? Code, string? Name);

public sealed record SetAssignmentsRequest(IReadOnlyList<Guid>? GroupIds, IReadOnlyList<Guid>? UserIds);

public sealed record SetUserOverrideRequest(int ExtraAttempts, string? Note);

internal static class ExamRules
{
    public const string CodePattern = "^[A-Za-z0-9._-]{2,100}$";

    public static void AddDetailsRules<T>(AbstractValidator<T> validator)
        where T : ExamDetailsInput
    {
        validator.RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên đề thi.")
            .MaximumLength(300);
        validator.RuleFor(r => r.Description).MaximumLength(10_000);
        validator.RuleFor(r => r.Instructions).MaximumLength(10_000);
        validator.RuleFor(r => r.MaxAttempts).InclusiveBetween(Exam.MinAttempts, Exam.MaxAttemptsLimit)
            .WithErrorCode("MAX_ATTEMPTS_INVALID").WithMessage($"Số lượt thi phải từ {Exam.MinAttempts} đến {Exam.MaxAttemptsLimit}.");
        validator.RuleFor(r => r.AccessMode).IsInEnum();
        validator.RuleFor(r => r.RetakeScoringPolicy).IsInEnum();
        validator.RuleFor(r => r.EndAt).GreaterThan(r => r.StartAt).When(r => r.StartAt is not null && r.EndAt is not null)
            .WithErrorCode("SCHEDULE_INVALID").WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
    }

    public static void AddSettingsRules<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, int>> duration,
        System.Linq.Expressions.Expression<Func<T, decimal?>> pass)
    {
        validator.RuleFor(duration).InclusiveBetween(ExamVersion.MinDuration, ExamVersion.MaxDuration)
            .WithErrorCode("DURATION_INVALID")
            .WithMessage($"Thời lượng phải từ {ExamVersion.MinDuration} đến {ExamVersion.MaxDuration} phút.");
        validator.RuleFor(pass).InclusiveBetween(0m, 100m).WithErrorCode("PASS_PERCENTAGE_INVALID")
            .WithMessage("Tỉ lệ đạt phải từ 0 đến 100.");
    }
}

internal sealed class CreateExamRequestValidator : AbstractValidator<CreateExamRequest>
{
    public CreateExamRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().WithErrorCode("CODE_REQUIRED").WithMessage("Vui lòng nhập mã đề.")
            .Matches(ExamRules.CodePattern).WithErrorCode("CODE_INVALID")
            .WithMessage("Mã đề gồm 2–100 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang.");
        ExamRules.AddDetailsRules(this);
        ExamRules.AddSettingsRules(this, r => r.DurationMinutes, r => r.PassPercentage);
        RuleFor(r => r.ScoreVisibility).IsInEnum();
        RuleFor(r => r.ReviewPolicy).IsInEnum();
    }
}

internal sealed class UpdateExamRequestValidator : AbstractValidator<UpdateExamRequest>
{
    public UpdateExamRequestValidator()
    {
        RuleFor(r => r.Code).Matches(ExamRules.CodePattern).When(r => !string.IsNullOrWhiteSpace(r.Code))
            .WithErrorCode("CODE_INVALID").WithMessage("Mã đề không hợp lệ.");
        ExamRules.AddDetailsRules(this);
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

internal sealed class UpdateVersionRequestValidator : AbstractValidator<UpdateVersionRequest>
{
    public UpdateVersionRequestValidator()
    {
        ExamRules.AddSettingsRules(this, r => r.DurationMinutes, r => r.PassPercentage);
        RuleFor(r => r.ScoreVisibility).IsInEnum();
        RuleFor(r => r.ReviewPolicy).IsInEnum();
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

internal sealed class AddVersionQuestionsRequestValidator : AbstractValidator<AddVersionQuestionsRequest>
{
    public AddVersionQuestionsRequestValidator()
    {
        RuleFor(r => r.QuestionIds).NotEmpty().WithErrorCode("QUESTION_IDS_REQUIRED").WithMessage("Vui lòng chọn câu hỏi.")
            .Must(ids => ids.Count <= ExamVersion.MaxQuestions)
            .WithErrorCode("TOO_MANY_QUESTIONS").WithMessage($"Tối đa {ExamVersion.MaxQuestions} câu mỗi lần.");
        RuleFor(r => r.Score).Must(s => s is null || (s is >= 0.25m and <= 100m && s * 4 % 1 == 0))
            .WithErrorCode("SCORE_INVALID").WithMessage("Điểm phải từ 0,25 đến 100 và là bội số của 0,25.");
    }
}

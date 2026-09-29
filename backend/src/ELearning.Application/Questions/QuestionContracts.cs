using ELearning.Domain.Enums;
using ELearning.Domain.Grading;
using ELearning.Domain.Questions;
using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Questions;

public sealed record QuestionListQuery : PageRequest
{
    public Guid? CategoryId { get; init; }

    public QuestionType? QuestionType { get; init; }

    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }

    public QuestionDifficulty? Difficulty { get; init; }

    public string? Tag { get; init; }
}

public sealed record QuestionListItemDto(
    Guid Id,
    string Code,
    string ContentPreview,
    QuestionType QuestionType,
    AnswerDataType? AnswerDataType,
    Guid? CategoryId,
    string? CategoryName,
    decimal DefaultScore,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    QuestionDifficulty? Difficulty = null,
    IReadOnlyList<string>? Tags = null);

public sealed record QuestionOptionDto(Guid Id, string OptionCode, string Content, bool IsCorrect, int DisplayOrder);

public sealed record QuestionDetailDto(
    Guid Id,
    string Code,
    Guid? CategoryId,
    string? CategoryName,
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
    bool IsActive,
    IReadOnlyList<QuestionOptionDto> Options,
    IReadOnlyList<string> AcceptedAnswers,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion,
    QuestionDifficulty? Difficulty,
    IReadOnlyList<string> Tags,
    bool PartialScoring,
    IReadOnlyDictionary<string, string>? Media = null);

public sealed record QuestionOptionInput(string? OptionCode, string Content, bool IsCorrect);

/// <summary>Body tạo / sửa câu hỏi (docs/05-api.md mục 6.3).</summary>
public record QuestionInput
{
    public Guid? CategoryId { get; init; }

    /// <summary>Để trống khi tạo mới thì hệ thống tự sinh "Q000123". Không đổi được khi sửa.</summary>
    public string? Code { get; init; }

    public string Content { get; init; } = string.Empty;

    public ContentFormat ContentFormat { get; init; } = ContentFormat.Plain;

    public QuestionType QuestionType { get; init; }

    public AnswerDataType? AnswerDataType { get; init; }

    public decimal DefaultScore { get; init; } = 1;

    public string? Explanation { get; init; }

    public IReadOnlyList<QuestionOptionInput>? Options { get; init; }

    public IReadOnlyList<string>? AcceptedAnswers { get; init; }

    public decimal? CorrectAnswerNumber { get; init; }

    public decimal? NumericTolerance { get; init; }

    public bool CaseSensitive { get; init; }

    public bool IgnoreAccent { get; init; }

    public QuestionDifficulty? Difficulty { get; init; }

    /// <summary>Chỉ câu chọn nhiều: chấm từng phần (docs/02-nghiep-vu.md mục 2).</summary>
    public bool PartialScoring { get; init; }

    /// <summary>Tối đa 10 tag, mỗi tag tối đa 50 ký tự; lưu dạng chữ thường.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record UpdateQuestionRequest : QuestionInput
{
    public string RowVersion { get; init; } = string.Empty;
}

public sealed record SetActiveRequest(bool IsActive);

/// <summary>Quy tắc hợp lệ (docs/02-nghiep-vu.md mục 1.1). Mã lỗi theo từng trường để UI hiển thị.</summary>
internal class QuestionInputValidator<T> : AbstractValidator<T>
    where T : QuestionInput
{
    public const int MaxContentLength = 10_000;
    public const int MaxOptionLength = 2_000;
    public const int MaxAcceptedAnswerLength = 1_000;

    private static readonly string[] ChoiceCodes = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];

    public QuestionInputValidator()
    {
        RuleFor(q => q.Code).Matches("^[A-Za-z0-9._-]{1,100}$").When(q => !string.IsNullOrWhiteSpace(q.Code))
            .WithErrorCode("CODE_INVALID").WithMessage("Mã câu hỏi gồm tối đa 100 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang.");
        RuleFor(q => q.Content).NotEmpty().WithErrorCode("CONTENT_REQUIRED").WithMessage("Vui lòng nhập nội dung câu hỏi.")
            .MaximumLength(MaxContentLength).WithErrorCode("CONTENT_TOO_LONG")
            .WithMessage($"Nội dung câu hỏi tối đa {MaxContentLength} ký tự.");
        RuleFor(q => q.ContentFormat).IsInEnum();
        RuleFor(q => q.QuestionType).IsInEnum().WithErrorCode("QUESTION_TYPE_INVALID").WithMessage("Loại câu hỏi không hợp lệ.");
        RuleFor(q => q.Explanation).MaximumLength(MaxContentLength);
        RuleFor(q => q.Difficulty).IsInEnum().When(q => q.Difficulty is not null)
            .WithErrorCode("DIFFICULTY_INVALID").WithMessage("Độ khó phải là EASY, MEDIUM hoặc HARD.");
        RuleFor(q => q.Tags).Must(t => t is null || t.Count(x => !string.IsNullOrWhiteSpace(x)) <= Question.MaxTags)
            .WithErrorCode("TAGS_TOO_MANY").WithMessage($"Tối đa {Question.MaxTags} tag.");
        RuleForEach(q => q.Tags).Must(t => t is null || t.Trim().Length <= Question.MaxTagLength)
            .WithErrorCode("TAG_TOO_LONG").WithMessage($"Mỗi tag tối đa {Question.MaxTagLength} ký tự.");
        RuleFor(q => q.DefaultScore).InclusiveBetween(0.25m, 100m).WithErrorCode("SCORE_OUT_OF_RANGE")
            .WithMessage("Điểm phải từ 0,25 đến 100.")
            .Must(s => s * 4 % 1 == 0).WithErrorCode("SCORE_STEP_INVALID").WithMessage("Điểm phải là bội số của 0,25.");

        When(q => q.QuestionType is QuestionType.SingleChoice or QuestionType.MultipleChoice, () =>
        {
            RuleFor(q => q.Options).NotNull().WithErrorCode("OPTIONS_REQUIRED").WithMessage("Vui lòng nhập các lựa chọn.")
                .Must(o => o!.Count is >= Question.MinOptions and <= Question.MaxOptions)
                .WithErrorCode("OPTIONS_COUNT_INVALID")
                .WithMessage($"Câu trắc nghiệm phải có từ {Question.MinOptions} đến {Question.MaxOptions} lựa chọn.");
            RuleForEach(q => q.Options).ChildRules(option =>
            {
                option.RuleFor(o => o.Content).NotEmpty().WithErrorCode("OPTION_CONTENT_REQUIRED")
                    .WithMessage("Nội dung lựa chọn không được để trống.")
                    .MaximumLength(MaxOptionLength).WithErrorCode("OPTION_CONTENT_TOO_LONG")
                    .WithMessage($"Nội dung lựa chọn tối đa {MaxOptionLength} ký tự.");
                option.RuleFor(o => o.OptionCode).Must(c => c is null || ChoiceCodes.Contains(c.Trim().ToUpperInvariant()))
                    .WithErrorCode("OPTION_CODE_INVALID").WithMessage("Mã lựa chọn phải là chữ cái từ A đến J.");
            });
            RuleFor(q => q.Options).Must(o => o is null || o.Count(x => x.IsCorrect) == 1)
                .When(q => q.QuestionType == QuestionType.SingleChoice)
                .WithErrorCode("SINGLE_CHOICE_REQUIRES_ONE_CORRECT").WithMessage("Câu chọn một phải có đúng 1 đáp án đúng.");
            RuleFor(q => q.Options).Must(o => o is null || o.Any(x => x.IsCorrect))
                .When(q => q.QuestionType == QuestionType.MultipleChoice)
                .WithErrorCode("MULTIPLE_CHOICE_REQUIRES_CORRECT").WithMessage("Câu chọn nhiều phải có ít nhất 1 đáp án đúng.");
            RuleFor(q => q.Options).Must(HaveUniqueCodes)
                .WithErrorCode("OPTION_CODE_DUPLICATE").WithMessage("Mã lựa chọn bị trùng.");
        });

        When(q => q.QuestionType == QuestionType.TrueFalse, () =>
            RuleFor(q => q.Options).NotNull().WithErrorCode("OPTIONS_REQUIRED").WithMessage("Vui lòng chọn đáp án đúng.")
                .Must(o => o!.Count == 2
                    && o.Select(x => x.OptionCode?.Trim().ToUpperInvariant()).OrderBy(c => c).SequenceEqual([Question.FalseCode, Question.TrueCode])
                    && o.Count(x => x.IsCorrect) == 1)
                .WithErrorCode("TRUE_FALSE_INVALID")
                .WithMessage("Câu Đúng/Sai phải có 2 lựa chọn TRUE, FALSE và đúng 1 đáp án đúng."));

        When(q => q.QuestionType == QuestionType.Essay, () =>
        {
            RuleFor(q => q.Options).Must(o => o is null || o.Count == 0)
                .WithErrorCode("ESSAY_HAS_NO_OPTIONS").WithMessage("Câu tự luận không có lựa chọn.");
            RuleFor(q => q.AcceptedAnswers).Must(a => a is null || a.All(string.IsNullOrWhiteSpace))
                .WithErrorCode("ESSAY_HAS_NO_ANSWER_KEY").WithMessage("Câu tự luận không có đáp án chấp nhận; dùng phần giải thích làm đáp án mẫu.");
            RuleFor(q => q.AnswerDataType).Null()
                .WithErrorCode("ESSAY_HAS_NO_DATA_TYPE").WithMessage("Câu tự luận không có kiểu đáp án.");
        });

        When(q => q.QuestionType == QuestionType.FillIn, () =>
        {
            RuleFor(q => q.AnswerDataType).NotNull().WithErrorCode("ANSWER_DATA_TYPE_REQUIRED")
                .WithMessage("Vui lòng chọn kiểu đáp án (TEXT hoặc NUMBER).").IsInEnum();
            RuleFor(q => q.Options).Must(o => o is null || o.Count == 0)
                .WithErrorCode("FILL_IN_HAS_NO_OPTIONS").WithMessage("Câu điền không có lựa chọn.");

            When(q => q.AnswerDataType == AnswerDataType.Text, () =>
            {
                RuleFor(q => q.AcceptedAnswers).NotNull().WithErrorCode("ACCEPTED_ANSWERS_REQUIRED")
                    .WithMessage("Vui lòng nhập ít nhất 1 đáp án chấp nhận.")
                    .Must(a => a!.Count is >= 1 and <= Question.MaxAcceptedAnswers).WithErrorCode("ACCEPTED_ANSWERS_COUNT_INVALID")
                    .WithMessage($"Câu điền chữ có từ 1 đến {Question.MaxAcceptedAnswers} đáp án chấp nhận.");
                RuleForEach(q => q.AcceptedAnswers)
                    .Must(a => AnswerNormalizer.Normalize(a, true, false).Length > 0).WithErrorCode("ACCEPTED_ANSWER_EMPTY")
                    .WithMessage("Đáp án chấp nhận không được để trống.")
                    .MaximumLength(MaxAcceptedAnswerLength);
                RuleFor(q => q.AcceptedAnswers).Must((q, answers) => answers is null || answers
                        .Select(a => AnswerNormalizer.Normalize(a, q.CaseSensitive, q.IgnoreAccent))
                        .Distinct(StringComparer.Ordinal).Count() == answers.Count)
                    .WithErrorCode("ACCEPTED_ANSWER_DUPLICATE")
                    .WithMessage("Có đáp án chấp nhận trùng nhau sau khi chuẩn hóa.");
            });

            When(q => q.AnswerDataType == AnswerDataType.Number, () =>
            {
                RuleFor(q => q.CorrectAnswerNumber).NotNull().WithErrorCode("CORRECT_NUMBER_REQUIRED")
                    .WithMessage("Vui lòng nhập đáp án số.");
                RuleFor(q => q.NumericTolerance).GreaterThanOrEqualTo(0).WithErrorCode("TOLERANCE_NEGATIVE")
                    .WithMessage("Sai số không được âm.");
            });
        });
    }

    private static bool HaveUniqueCodes(IReadOnlyList<QuestionOptionInput>? options)
    {
        if (options is null)
        {
            return true;
        }

        var codes = options.Where(o => o.OptionCode is not null).Select(o => o.OptionCode!.Trim().ToUpperInvariant()).ToList();
        return codes.Distinct(StringComparer.Ordinal).Count() == codes.Count;
    }
}

internal sealed class CreateQuestionValidator : QuestionInputValidator<QuestionInput>;

internal sealed class UpdateQuestionValidator : QuestionInputValidator<UpdateQuestionRequest>
{
    public UpdateQuestionValidator() =>
        RuleFor(q => q.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
}

using ELearning.Domain.Enums;
using ELearning.Domain.Exams;

namespace ELearning.Domain.Grading;

/// <summary>Dữ liệu chấm của một câu, dựng từ snapshot ExamQuestion (D-01) — không bao giờ từ bảng Questions.</summary>
public sealed record GradingQuestion(
    QuestionType QuestionType,
    AnswerDataType? AnswerDataType,
    decimal Score,
    bool IsVoided,
    IReadOnlySet<string> CorrectOptionCodes,
    IReadOnlyList<string> AcceptedAnswers,
    decimal? CorrectAnswerNumber,
    decimal? NumericTolerance,
    bool CaseSensitive,
    bool IgnoreAccent,
    bool PartialScoring = false)
{
    public static GradingQuestion From(ExamQuestion q) => new(
        q.QuestionType,
        q.AnswerDataType,
        q.Score,
        q.IsVoided,
        q.Options.Where(o => o.IsCorrect).Select(o => o.OptionCode).ToHashSet(StringComparer.Ordinal),
        q.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
        q.CorrectAnswerNumber,
        q.NumericTolerance,
        q.CaseSensitive,
        q.IgnoreAccent,
        q.PartialScoring);
}

/// <summary>Câu trả lời của học viên cho một câu.</summary>
/// <param name="ManualScore">Điểm chấm tay (câu tự luận); null = chưa chấm.</param>
public sealed record StudentAnswer(IReadOnlyCollection<string> SelectedOptionCodes, string? AnswerText, decimal? AnswerNumber, decimal? ManualScore = null)
{
    public static readonly StudentAnswer Empty = new([], null, null);

    public bool IsAnswered => SelectedOptionCodes.Count > 0 || !string.IsNullOrWhiteSpace(AnswerText);
}

/// <param name="NeedsManualGrading">Câu tự luận có trả lời nhưng chưa được chấm tay: tạm tính 0 điểm.</param>
public sealed record GradingResult(bool IsCorrect, decimal Score, decimal MaxScore, bool NeedsManualGrading = false)
{
    public static GradingResult Correct(decimal max) => new(true, max, max);

    public static GradingResult Wrong(decimal max) => new(false, 0m, max);
}

/// <summary>Chiến lược chấm theo loại câu (docs/03-kien-truc.md mục 5). Mỗi câu: đúng = điểm tối đa, sai = 0.</summary>
public interface IQuestionGrader
{
    QuestionType Type { get; }

    GradingResult Grade(GradingQuestion question, StudentAnswer answer);
}

public sealed class SingleChoiceGrader : IQuestionGrader
{
    public QuestionType Type => QuestionType.SingleChoice;

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer) =>
        answer.SelectedOptionCodes.Count == 1 && question.CorrectOptionCodes.Count == 1
            && question.CorrectOptionCodes.Contains(answer.SelectedOptionCodes.First())
            ? GradingResult.Correct(question.Score)
            : GradingResult.Wrong(question.Score);
}

public sealed class TrueFalseGrader : IQuestionGrader
{
    private readonly SingleChoiceGrader _single = new();

    public QuestionType Type => QuestionType.TrueFalse;

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer) => _single.Grade(question, answer);
}

/// <summary>Đúng khi tập đã chọn bằng đúng tập đáp án (không phụ thuộc thứ tự, docs/02-nghiep-vu.md mục 2).</summary>
public sealed class MultipleChoiceGrader : IQuestionGrader
{
    public QuestionType Type => QuestionType.MultipleChoice;

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer)
    {
        var selected = answer.SelectedOptionCodes.ToHashSet(StringComparer.Ordinal);
        if (selected.Count > 0 && selected.SetEquals(question.CorrectOptionCodes))
        {
            return GradingResult.Correct(question.Score);
        }

        if (!question.PartialScoring || question.CorrectOptionCodes.Count == 0)
        {
            return GradingResult.Wrong(question.Score);
        }

        // Chấm từng phần: (số lựa chọn đúng đã chọn − số lựa chọn sai đã chọn) / tổng số đáp án đúng, không âm
        var right = selected.Count(question.CorrectOptionCodes.Contains);
        var wrong = selected.Count - right;
        var ratio = Math.Max(0, right - wrong) / (decimal)question.CorrectOptionCodes.Count;
        var score = Math.Round(question.Score * ratio, 2, MidpointRounding.AwayFromZero);
        return new GradingResult(false, score, question.Score);
    }
}

/// <summary>Tự luận: bỏ trống → 0 điểm; có trả lời → dùng điểm chấm tay, chưa chấm thì chờ chấm (tạm 0 điểm).</summary>
public sealed class EssayGrader : IQuestionGrader
{
    public QuestionType Type => QuestionType.Essay;

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer)
    {
        if (!answer.IsAnswered)
        {
            return GradingResult.Wrong(question.Score);
        }

        return answer.ManualScore is { } manual
            ? new GradingResult(manual >= question.Score, Math.Min(manual, question.Score), question.Score)
            : new GradingResult(false, 0m, question.Score, NeedsManualGrading: true);
    }
}

/// <summary>Câu điền: TEXT so sánh sau chuẩn hóa với một trong các đáp án chấp nhận; NUMBER so theo sai số.</summary>
public sealed class FillInGrader : IQuestionGrader
{
    public QuestionType Type => QuestionType.FillIn;

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer)
    {
        var correct = question.AnswerDataType switch
        {
            AnswerDataType.Text => !string.IsNullOrWhiteSpace(answer.AnswerText)
                && question.AcceptedAnswers.Any(a =>
                    AnswerNormalizer.AreEquivalent(a, answer.AnswerText, question.CaseSensitive, question.IgnoreAccent)),
            AnswerDataType.Number => question.CorrectAnswerNumber is { } expected
                && ParseNumber(answer) is { } actual
                && NumericAnswerParser.IsWithinTolerance(actual, expected, question.NumericTolerance ?? 0m),
            _ => false,
        };

        return correct ? GradingResult.Correct(question.Score) : GradingResult.Wrong(question.Score);
    }

    private static decimal? ParseNumber(StudentAnswer answer)
    {
        if (answer.AnswerNumber is { } value)
        {
            return value;
        }

        return NumericAnswerParser.TryParse(answer.AnswerText, out var parsed) ? parsed : null;
    }
}

/// <summary>Chọn grader theo loại câu; câu bị hủy (D-11) được điểm tối đa trước khi gọi grader.</summary>
public sealed class GradingEngine
{
    private readonly Dictionary<QuestionType, IQuestionGrader> _graders;

    public GradingEngine(IEnumerable<IQuestionGrader> graders)
    {
        _graders = graders.ToDictionary(g => g.Type);
        var missing = Enum.GetValues<QuestionType>().Where(t => !_graders.ContainsKey(t)).ToList();
        if (missing.Count > 0)
        {
            // Fail fast khi khởi động nếu thêm loại câu mà quên grader
            throw new InvalidOperationException($"Thiếu grader cho: {string.Join(", ", missing)}");
        }
    }

    public static GradingEngine Default { get; } =
        new([new SingleChoiceGrader(), new MultipleChoiceGrader(), new TrueFalseGrader(), new FillInGrader(), new EssayGrader()]);

    public GradingResult Grade(GradingQuestion question, StudentAnswer answer) =>
        question.IsVoided ? GradingResult.Correct(question.Score) : _graders[question.QuestionType].Grade(question, answer);
}

/// <summary>Tổng kết một lượt thi (docs/02-nghiep-vu.md mục 2). Dùng decimal, làm tròn AwayFromZero.</summary>
/// <param name="PendingManualCount">Số câu tự luận chờ chấm tay; khác 0 thì điểm là tạm tính và Passed = null.</param>
public sealed record AttemptScore(
    int TotalQuestion, int AnsweredCount, int CorrectCount, decimal TotalScore, decimal MaxScore, decimal Percentage, bool? Passed,
    int PendingManualCount = 0)
{
    public static AttemptScore Calculate(
        IReadOnlyCollection<(GradingResult Result, bool IsAnswered)> questions, decimal? passPercentage)
    {
        var max = questions.Sum(q => q.Result.MaxScore);
        var total = questions.Sum(q => q.Result.Score);
        var percentage = max == 0 ? 0 : Math.Round(total / max * 100m, 2, MidpointRounding.AwayFromZero);
        var pending = questions.Count(q => q.Result.NeedsManualGrading);
        return new AttemptScore(
            questions.Count,
            questions.Count(q => q.IsAnswered),
            questions.Count(q => q.Result.IsCorrect),
            total,
            max,
            percentage,
            pending == 0 && passPercentage is { } pass ? percentage >= pass : null,
            pending);
    }
}

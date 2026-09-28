using ELearning.Domain.Enums;
using ELearning.Domain.Grading;

namespace ELearning.UnitTests.Grading;

/// <summary>Các ca chấm điểm bắt buộc (docs/08-kiem-thu.md mục 2).</summary>
public class GradingTests
{
    private readonly GradingEngine _engine = GradingEngine.Default;

    [Theory]
    [InlineData(new[] { "B" }, true)]
    [InlineData(new[] { "A" }, false)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "A", "B" }, false)]
    [InlineData(new[] { "Z" }, false)]
    public void Single_choice(string[] selected, bool correct) =>
        Assert(Choice(QuestionType.SingleChoice, "B"), Choices(selected), correct);

    [Theory]
    [InlineData(new[] { "A", "C", "D" }, true)]
    [InlineData(new[] { "D", "A", "C" }, true)]
    [InlineData(new[] { "A", "C" }, false)]
    [InlineData(new[] { "A", "B", "C", "D" }, false)]
    [InlineData(new string[0], false)]
    public void Multiple_choice_requires_exact_set(string[] selected, bool correct) =>
        Assert(Choice(QuestionType.MultipleChoice, "A", "C", "D"), Choices(selected), correct);

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    public void True_false(string selected, bool correct) =>
        Assert(Choice(QuestionType.TrueFalse, "TRUE"), Choices([selected]), correct);

    [Fact]
    public void True_false_unanswered_is_wrong() =>
        Assert(Choice(QuestionType.TrueFalse, "TRUE"), StudentAnswer.Empty, false);

    [Theory]
    [InlineData("Hà Nội", true)]
    [InlineData("  hà   NỘI ", true)]
    [InlineData("Thành phố Hà Nội", true)]
    [InlineData("Ha Noi", false)]
    [InlineData("Huế", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Fill_text_matches_any_accepted_answer(string? input, bool correct) =>
        Assert(Text(["Hà Nội", "Thành phố Hà Nội"]), new StudentAnswer([], input, null), correct);

    [Fact]
    public void Fill_text_nfd_input_matches_nfc_answer() =>
        Assert(Text(["Hà Nội"]), new StudentAnswer([], "Hà Nội".Normalize(System.Text.NormalizationForm.FormD), null), true);

    [Fact]
    public void Fill_text_ignore_accent_and_case_sensitive_options()
    {
        Assert(Text(["Đà Nẵng"], ignoreAccent: true), new StudentAnswer([], "da nang", null), true);
        Assert(Text(["Async"], caseSensitive: true), new StudentAnswer([], "async", null), false);
        Assert(Text(["Async"], caseSensitive: true), new StudentAnswer([], "Async", null), true);
    }

    [Theory]
    [InlineData("4", true)]
    [InlineData("4,05", true)]
    [InlineData("4.1", true)]
    [InlineData("4.11", false)]
    [InlineData("3,9", true)]
    [InlineData("abc", false)]
    [InlineData(null, false)]
    public void Fill_number_uses_tolerance(string? input, bool correct) =>
        Assert(Number(4m, 0.1m), new StudentAnswer([], input, null), correct);

    [Fact]
    public void Fill_number_prefers_parsed_value()
    {
        Assert(Number(3.5m, 0m), new StudentAnswer([], "3,5", 3.5m), true);
        Assert(Number(3.5m, 0m), new StudentAnswer([], "3,5", null), true);
    }

    [Fact]
    public void Voided_question_gives_full_score_even_when_unanswered()
    {
        var question = Choice(QuestionType.SingleChoice, "A") with { IsVoided = true, Score = 2m };

        var result = _engine.Grade(question, StudentAnswer.Empty);

        result.Should().Be(new GradingResult(true, 2m, 2m));
    }

    [Fact]
    public void Engine_requires_a_grader_for_every_type()
    {
        var act = () => new GradingEngine([new SingleChoiceGrader()]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*MultipleChoice*");
    }

    [Fact]
    public void Attempt_score_totals_percentage_and_pass()
    {
        var results = new List<(GradingResult, bool)>
        {
            (GradingResult.Correct(1m), true),
            (GradingResult.Correct(2m), true),
            (GradingResult.Wrong(3m), false),
        };

        var score = AttemptScore.Calculate(results, passPercentage: 50m);

        score.Should().Be(new AttemptScore(3, 2, 2, 3m, 6m, 50.00m, true), "đạt khi bằng đúng ngưỡng");
        AttemptScore.Calculate(results, null).Passed.Should().BeNull();
    }

    [Fact]
    public void Percentage_rounds_half_away_from_zero()
    {
        // 1/3 = 33.333… → 33.33; 2/3 = 66.666… → 66.67
        AttemptScore.Calculate([(GradingResult.Correct(1m), true), (GradingResult.Wrong(2m), true)], null)
            .Percentage.Should().Be(33.33m);
        AttemptScore.Calculate([(GradingResult.Correct(2m), true), (GradingResult.Wrong(1m), true)], null)
            .Percentage.Should().Be(66.67m);
        // 0.125 → 0.13 (AwayFromZero), không phải 0.12 (ToEven)
        AttemptScore.Calculate([(GradingResult.Correct(0.25m), true), (GradingResult.Wrong(199.75m), true)], null)
            .Percentage.Should().Be(0.13m);
    }

    private void Assert(GradingQuestion question, StudentAnswer answer, bool correct)
    {
        var result = _engine.Grade(question, answer);
        result.IsCorrect.Should().Be(correct);
        result.Score.Should().Be(correct ? question.Score : 0m);
        result.MaxScore.Should().Be(question.Score);
    }

    private static StudentAnswer Choices(string[] selected) => new(selected, null, null);

    private static GradingQuestion Choice(QuestionType type, params string[] correct) =>
        new(type, null, 1m, false, correct.ToHashSet(StringComparer.Ordinal), [], null, null, false, false);

    private static GradingQuestion Text(string[] accepted, bool caseSensitive = false, bool ignoreAccent = false) =>
        new(QuestionType.FillIn, AnswerDataType.Text, 1m, false, new HashSet<string>(), accepted, null, null, caseSensitive, ignoreAccent);

    private static GradingQuestion Number(decimal answer, decimal tolerance) =>
        new(QuestionType.FillIn, AnswerDataType.Number, 1m, false, new HashSet<string>(), [], answer, tolerance, false, false);
}

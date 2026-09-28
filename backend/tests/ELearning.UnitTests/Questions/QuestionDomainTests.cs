using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Questions;

namespace ELearning.UnitTests.Questions;

public class QuestionDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void Single_choice_requires_exactly_one_correct_option()
    {
        Question.Validate(Choice(QuestionType.SingleChoice, ("A", true), ("B", true))).Should().NotBeEmpty();
        Question.Validate(Choice(QuestionType.SingleChoice, ("A", false), ("B", false))).Should().NotBeEmpty();
        Question.Validate(Choice(QuestionType.SingleChoice, ("A", true), ("B", false))).Should().BeEmpty();
    }

    [Fact]
    public void Multiple_choice_requires_at_least_one_correct_and_two_options()
    {
        Question.Validate(Choice(QuestionType.MultipleChoice, ("A", false), ("B", false))).Should().NotBeEmpty();
        Question.Validate(Choice(QuestionType.MultipleChoice, ("A", true))).Should().NotBeEmpty();
        Question.Validate(Choice(QuestionType.MultipleChoice, ("A", true), ("B", true), ("C", false))).Should().BeEmpty();
    }

    [Fact]
    public void True_false_requires_true_and_false_codes()
    {
        Question.Validate(Choice(QuestionType.TrueFalse, ("A", true), ("B", false))).Should().NotBeEmpty();
        Question.Validate(Choice(QuestionType.TrueFalse, ("TRUE", true), ("FALSE", false))).Should().BeEmpty();
    }

    [Fact]
    public void Fill_in_text_requires_accepted_answers()
    {
        Question.Validate(FillText()).Should().NotBeEmpty();
        Question.Validate(FillText("   ")).Should().NotBeEmpty();
        Question.Validate(FillText("Hà Nội")).Should().BeEmpty();
    }

    [Fact]
    public void Fill_in_number_requires_answer_and_non_negative_tolerance()
    {
        Question.Validate(FillNumber(null, 0)).Should().NotBeEmpty();
        Question.Validate(FillNumber(4, -1)).Should().NotBeEmpty();
        Question.Validate(FillNumber(4, 0)).Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_invalid_data_with_domain_exception()
    {
        var act = () => Question.Create("Q1", Choice(QuestionType.SingleChoice, ("A", true), ("B", true)), Admin, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.InvalidQuestion);
    }

    [Fact]
    public void Update_replaces_options_and_clears_fields_of_other_types()
    {
        var question = Question.Create("Q1", FillNumber(4, 0.5m), Admin, Now);
        question.CorrectAnswerNumber.Should().Be(4);

        question.Update(Choice(QuestionType.SingleChoice, ("A", false), ("B", true), ("C", false)), Admin, Now);

        question.QuestionType.Should().Be(QuestionType.SingleChoice);
        question.AnswerDataType.Should().BeNull();
        question.CorrectAnswerNumber.Should().BeNull();
        question.NumericTolerance.Should().BeNull();
        question.Options.Select(o => (o.OptionCode, o.DisplayOrder)).Should().Equal(("A", 1), ("B", 2), ("C", 3));
    }

    [Fact]
    public void Content_is_cleaned_to_nfc_without_control_characters()
    {
        var data = FillText("Hà Nội") with { Content = "  Thủ đô\u0000 Việt Nam?  ".Normalize(System.Text.NormalizationForm.FormD) };

        var question = Question.Create("Q1", data, Admin, Now);

        question.Content.Should().Be("Thủ đô Việt Nam?");
    }

    [Fact]
    public void Clone_copies_everything_with_new_code()
    {
        var source = Question.Create("Q1", Choice(QuestionType.MultipleChoice, ("A", true), ("B", false), ("C", true)), Admin, Now);

        var clone = source.Clone("Q2", Admin, Now);

        clone.Code.Should().Be("Q2");
        clone.IsActive.Should().BeTrue();
        clone.ToData().Should().BeEquivalentTo(source.ToData());
    }

    private static QuestionData Choice(QuestionType type, params (string Code, bool Correct)[] options) =>
        new(null, "Nội dung", ContentFormat.Plain, type, null, null, null, false, false, null, 1m,
            options.Select(o => new OptionData(o.Code, $"Lựa chọn {o.Code}", o.Correct)).ToList(), []);

    private static QuestionData FillText(params string[] accepted) =>
        new(null, "Thủ đô Việt Nam?", ContentFormat.Plain, QuestionType.FillIn, AnswerDataType.Text, null, null, false, false,
            null, 1m, [], accepted);

    private static QuestionData FillNumber(decimal? answer, decimal tolerance) =>
        new(null, "2 + 2 = ?", ContentFormat.Plain, QuestionType.FillIn, AnswerDataType.Number, answer, tolerance, false, false,
            null, 1m, [], []);
}

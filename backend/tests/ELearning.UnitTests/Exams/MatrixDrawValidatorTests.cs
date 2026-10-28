using ELearning.Application.Exams;
using ELearning.Domain.Enums;
using FluentValidation.TestHelper;

namespace ELearning.UnitTests.Exams;

public class MatrixDrawValidatorTests
{
    private readonly MatrixDrawRequestValidator _validator = new();

    [Fact]
    public void Valid_static_request_passes()
    {
        var request = new MatrixDrawRequest(
            CategoryId: Guid.NewGuid(),
            Tag: null,
            QuestionType: QuestionType.SingleChoice,
            EasyCount: 2,
            MediumCount: 1,
            HardCount: 1,
            ScorePerQuestion: 2.5m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Zero_total_count_fails()
    {
        var request = new MatrixDrawRequest(
            CategoryId: Guid.NewGuid(),
            Tag: null,
            QuestionType: null,
            EasyCount: 0,
            MediumCount: 0,
            HardCount: 0,
            ScorePerQuestion: 1m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("totalCount");
    }

    [Fact]
    public void Negative_counts_fail()
    {
        var request = new MatrixDrawRequest(
            CategoryId: null,
            Tag: null,
            QuestionType: null,
            EasyCount: -1,
            MediumCount: 2,
            HardCount: 0,
            ScorePerQuestion: 1m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.EasyCount);
    }

    [Fact]
    public void Pool_mode_requires_score()
    {
        var request = new MatrixDrawRequest(
            CategoryId: Guid.NewGuid(),
            Tag: null,
            QuestionType: null,
            EasyCount: 2,
            MediumCount: 1,
            HardCount: 1,
            ScorePerQuestion: null,
            Mode: MatrixDrawMode.Pool
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.ScorePerQuestion);
    }

    [Fact]
    public void Score_not_multiple_of_quarter_fails()
    {
        var request = new MatrixDrawRequest(
            CategoryId: Guid.NewGuid(),
            Tag: null,
            QuestionType: null,
            EasyCount: 2,
            MediumCount: 1,
            HardCount: 1,
            ScorePerQuestion: 1.33m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.ScorePerQuestion);
    }
}

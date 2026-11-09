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
            Level1Count: 2,
            Level2Count: 1,
            Level3Count: 1,
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
            Level1Count: 0,
            Level2Count: 0,
            Level3Count: 0,
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
            Level1Count: -1,
            Level2Count: 2,
            Level3Count: 0,
            ScorePerQuestion: 1m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Level1Count);
    }

    [Fact]
    public void Level4_counts_toward_total_and_cannot_be_negative()
    {
        var onlyLevel4 = new MatrixDrawRequest(null, null, null, 0, 0, 0, 1m, MatrixDrawMode.Static, Level4Count: 2);
        var negative = onlyLevel4 with { Level4Count = -1 };

        _validator.TestValidate(onlyLevel4).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(negative).ShouldHaveValidationErrorFor(r => r.Level4Count);
    }

    [Fact]
    public void Pool_mode_requires_score()
    {
        var request = new MatrixDrawRequest(
            CategoryId: Guid.NewGuid(),
            Tag: null,
            QuestionType: null,
            Level1Count: 2,
            Level2Count: 1,
            Level3Count: 1,
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
            Level1Count: 2,
            Level2Count: 1,
            Level3Count: 1,
            ScorePerQuestion: 1.33m,
            Mode: MatrixDrawMode.Static
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.ScorePerQuestion);
    }

    [Fact]
    public void Valid_multi_topic_request_passes()
    {
        var request = new MatrixDrawRequest(
            CategoryId: null,
            Tag: null,
            QuestionType: null,
            Level1Count: 0,
            Level2Count: 0,
            Level3Count: 0,
            ScorePerQuestion: 0.5m,
            Mode: MatrixDrawMode.Static,
            Topics:
            [
                new MatrixTopicItem(Guid.NewGuid(), Level1Count: 3, Level2Count: 2, Level3Count: 1),
                new MatrixTopicItem(Guid.NewGuid(), Level1Count: 2, Level2Count: 1, Level3Count: 1)
            ]
        );

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Multi_topic_zero_total_fails()
    {
        var request = new MatrixDrawRequest(
            CategoryId: null,
            Tag: null,
            QuestionType: null,
            Level1Count: 0,
            Level2Count: 0,
            Level3Count: 0,
            ScorePerQuestion: 1m,
            Mode: MatrixDrawMode.Static,
            Topics:
            [
                new MatrixTopicItem(Guid.NewGuid(), Level1Count: 0, Level2Count: 0, Level3Count: 0)
            ]
        );

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("totalCount");
    }
}

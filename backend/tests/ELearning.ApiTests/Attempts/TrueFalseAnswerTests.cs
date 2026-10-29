using System.Net;

namespace ELearning.ApiTests.Attempts;

/// <summary>Câu Đúng/Sai: kiểu cũ TRUE/FALSE và nhiều mệnh đề (docs/02-nghiep-vu.md mục 2). Tái hiện lỗi chọn cả Đúng lẫn Sai.</summary>
[Collection(ApiCollection.Name)]
public class TrueFalseAnswerTests(ApiFactory factory)
{
    private static readonly object Statements = new
    {
        content = "Cho hàm số $y = x^3$.",
        questionType = "TRUE_FALSE",
        options = new[]
        {
            new { optionCode = "A", content = "Đồng biến trên R", isCorrect = true },
            new { optionCode = "B", content = "Có cực đại", isCorrect = false },
            new { optionCode = "C", content = "Có cực tiểu", isCorrect = false },
            new { optionCode = "D", content = "Đi qua gốc tọa độ", isCorrect = true },
        },
    };

    [Fact]
    public async Task Classic_true_false_rejects_choosing_both_and_text_answers()
    {
        var (student, attempt) = await StartAsync();
        var classic = attempt.Questions.Single(q => q.Type == "TRUE_FALSE" && q.Options.Count == 2);

        var (both, bothBody) = await AttemptTestKit.SaveAsync(student, attempt,
            [AttemptTestKit.Merge(new { selectedOptions = new[] { "TRUE", "FALSE" } }, classic.Id, 1)]);
        var (text, textBody) = await AttemptTestKit.SaveAsync(student, attempt,
            [AttemptTestKit.Merge(new { answerText = "{\"TRUE\":true,\"FALSE\":true}" }, classic.Id, 2)]);

        both.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        bothBody.Errors.Should().Contain(e => e.Code == "INVALID_ANSWER_SHAPE");
        text.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        textBody.Errors.Should().Contain(e => e.Code == "INVALID_ANSWER_SHAPE");
    }

    [Fact]
    public async Task Statements_accept_only_well_formed_answers_and_score_answered_ones()
    {
        var (student, attempt) = await StartAsync();
        var question = attempt.Questions.Single(q => q.Type == "TRUE_FALSE" && q.Options.Count == 4);

        var (unknownKey, unknownBody) = await AttemptTestKit.SaveAsync(student, attempt,
            [AttemptTestKit.Merge(new { answerText = "{\"A\":true,\"Z\":false}" }, question.Id, 1)]);
        var (notBoolean, _) = await AttemptTestKit.SaveAsync(student, attempt,
            [AttemptTestKit.Merge(new { answerText = "{\"A\":\"đúng\"}" }, question.Id, 2)]);
        var (ok, _) = await AttemptTestKit.SaveAsync(student, attempt,
            [AttemptTestKit.Merge(new { answerText = "{\"c\":true,\"a\":true,\"b\":false}" }, question.Id, 3)]);
        var (_, reloaded) = await student.GetJsonAsync<AttemptView>($"/api/student/attempts/{attempt.AttemptId}");
        var (_, result) = await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        unknownKey.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownBody.Errors.Should().Contain(e => e.Code == "INVALID_ANSWER_SHAPE");
        notBoolean.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        reloaded.Data!.Questions.Single(q => q.Id == question.Id).Answer.AnswerText
            .Should().Be("{\"A\":true,\"B\":false,\"C\":true}", "lưu dạng chuẩn: mã của câu, theo thứ tự mệnh đề");

        // A đúng, B đúng, C sai, D bỏ trống (không được tính là "Sai") → 2/4 của 1 điểm; các câu khác bỏ trống
        result.Data!.TotalScore.Should().Be(0.5m);
        result.Data.Questions!.Single(q => q.Id == question.Id).IsCorrect.Should().BeFalse();
    }

    private async Task<(HttpClient Student, AttemptView Attempt)> StartAsync()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(extraQuestions: [Statements]);
        return (student, await AttemptTestKit.StartAsync(student, exam.Id));
    }
}

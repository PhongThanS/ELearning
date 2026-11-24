using System.Net;
using ELearning.ApiTests.Admin;

namespace ELearning.ApiTests.Attempts;

public sealed record HistoryRow(Guid AttemptId, Guid ExamId, int AttemptNumber, string Status);

/// <summary>Xóa đề đã có học sinh làm: báo trước, chặn khi đang có người thi, xóa kèm kết quả khi xác nhận.</summary>
[Collection(ApiCollection.Name)]
public class ExamDeleteTests(ApiFactory factory)
{
    [Fact]
    public async Task Exam_with_submitted_attempt_needs_force_and_force_removes_results()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1, durationMinutes: 30);
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        (await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit")).Item1.StatusCode.Should().Be(HttpStatusCode.OK);

        var (plain, plainBody) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/exams/{exam.Id}");
        var (forced, _) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/exams/{exam.Id}?force=true");
        var (after, _) = await kit.Admin.GetJsonAsync<object>($"/api/exams/{exam.Id}");
        var (_, history) = await student.GetJsonAsync<Paged<HistoryRow>>("/api/student/history");

        plain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        plainBody.Errors.Single().Code.Should().Be("EXAM_HAS_ATTEMPTS");
        forced.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
        history.Data!.Items.Should().NotContain(h => h.ExamId == exam.Id);
    }

    [Fact]
    public async Task Exam_with_in_progress_attempt_cannot_be_deleted_even_with_force()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1, durationMinutes: 30);
        await AttemptTestKit.StartAsync(student, exam.Id);

        var (resp, body) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/exams/{exam.Id}?force=true");

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Errors.Single().Code.Should().Be("EXAM_HAS_ACTIVE_ATTEMPTS");
    }

    [Fact]
    public async Task History_shows_only_latest_finished_attempt_of_practice_exam()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1, durationMinutes: 30, kind: "PRACTICE");
        for (var i = 0; i < 3; i++)
        {
            var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
            await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");
        }

        var (_, history) = await student.GetJsonAsync<Paged<HistoryRow>>("/api/student/history");

        history.Data!.Items.Where(h => h.ExamId == exam.Id).Should().ContainSingle().Which.AttemptNumber.Should().Be(3);
    }
}

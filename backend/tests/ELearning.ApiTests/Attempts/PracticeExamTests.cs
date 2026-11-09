using System.Net;
using ELearning.ApiTests.Admin;

namespace ELearning.ApiTests.Attempts;

public sealed record StudentExamKindItem(Guid ExamId, string Kind, int MaxAttempts, int DurationMinutes, int RemainingAttempts, string Availability);

public sealed record ExamKindDetail(Guid Id, string Kind, int MaxAttempts);

/// <summary>Đề luyện (D-33): làm lại không giới hạn, không tính giờ; đề tính điểm giữ nguyên quy tắc.</summary>
[Collection(ApiCollection.Name)]
public class PracticeExamTests(ApiFactory factory)
{
    [Fact]
    public async Task Practice_exam_has_no_time_limit_and_unlimited_retakes()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1, durationMinutes: 30, kind: "PRACTICE");

        var (_, detail) = await kit.Admin.GetJsonAsync<ExamKindDetail>($"/api/exams/{exam.Id}");
        detail.Data!.Kind.Should().Be("PRACTICE");

        for (var round = 1; round <= 3; round++)
        {
            var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
            attempt.AttemptNumber.Should().Be(round);
            (attempt.ExpiredAt - attempt.StartedAt).Should().BeGreaterThan(TimeSpan.FromDays(1), "đề luyện không tính giờ");
            var (submit, _) = await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");
            submit.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var (_, list) = await student.GetJsonAsync<Paged<StudentExamKindItem>>("/api/student/exams");
        list.Data!.Items.Should().ContainSingle(e => e.ExamId == exam.Id).Which.Should().Match<StudentExamKindItem>(e =>
            e.Kind == "PRACTICE" && e.MaxAttempts == 0 && e.DurationMinutes == 0 && e.RemainingAttempts == 1 && e.Availability == "AVAILABLE");
    }

    [Fact]
    public async Task Graded_exam_keeps_attempt_limit_and_duration()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1, durationMinutes: 30);

        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");
        var (again, againBody) = await student.PostJsonAsync<object>($"/api/student/exams/{exam.Id}/start");

        (attempt.ExpiredAt - attempt.StartedAt).Should().Be(TimeSpan.FromMinutes(30));
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        againBody.Errors.Should().Contain(e => e.Code == "MAX_ATTEMPTS_EXCEEDED");
    }
}

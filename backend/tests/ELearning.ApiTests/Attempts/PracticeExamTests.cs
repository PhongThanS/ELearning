using System.Net;
using ELearning.ApiTests.Admin;

namespace ELearning.ApiTests.Attempts;

public sealed record StudentExamKindItem(Guid ExamId, string Kind, int MaxAttempts, int DurationMinutes, int RemainingAttempts, string Availability);

public sealed record ExamKindDetail(Guid Id, string Kind, int MaxAttempts);

public sealed record ExamAccessDetail(Guid Id, string AccessMode, string RowVersion);

public sealed record ExamAccessAssignments(string AccessMode);

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
    public async Task Open_exam_cannot_change_kind_and_switches_access_mode_together_with_assignments()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, studentId, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(accessMode: "PUBLIC");
        var (_, current) = await kit.Admin.GetJsonAsync<ExamAccessDetail>($"/api/exams/{exam.Id}");

        var (kind, kindBody) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Id}", new
        {
            name = "Đề làm bài",
            maxAttempts = 1,
            accessMode = "PUBLIC",
            kind = "PRACTICE",
            rowVersion = current.Data!.RowVersion,
        });
        var assignmentsUrl = $"/api/exams/{exam.Id}/assignments";
        var (empty, emptyBody) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Put, assignmentsUrl, new { userIds = Array.Empty<Guid>(), accessMode = "ASSIGNED" });
        var (assigned, assignedBody) = await kit.Admin.SendJsonAsync<ExamAccessAssignments>(HttpMethod.Put, assignmentsUrl, new { userIds = new[] { studentId }, accessMode = "ASSIGNED" });
        var (_, afterAssign) = await kit.Admin.GetJsonAsync<ExamAccessDetail>($"/api/exams/{exam.Id}");
        var (backToPublic, _) = await kit.Admin.SendJsonAsync<ExamAccessAssignments>(HttpMethod.Put, assignmentsUrl, new { userIds = new[] { studentId }, accessMode = "PUBLIC" });
        var (_, afterPublic) = await kit.Admin.GetJsonAsync<ExamAccessDetail>($"/api/exams/{exam.Id}");

        kind.StatusCode.Should().Be(HttpStatusCode.Conflict, "đề đã mở không đổi được loại đề");
        kindBody.Errors.Should().Contain(e => e.Code == "EXAM_KIND_LOCKED");
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        emptyBody.Errors.Should().Contain(e => e.Code == "NO_ASSIGNMENTS");
        assigned.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", assignedBody.Errors.Select(e => e.Code)));
        assignedBody.Data!.AccessMode.Should().Be("ASSIGNED");
        afterAssign.Data!.AccessMode.Should().Be("ASSIGNED");
        backToPublic.StatusCode.Should().Be(HttpStatusCode.OK);
        afterPublic.Data!.AccessMode.Should().Be("PUBLIC");
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

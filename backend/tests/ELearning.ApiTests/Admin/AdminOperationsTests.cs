using System.Net;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Exams;

namespace ELearning.ApiTests.Admin;

public sealed record AdminAnswer(
    Guid AttemptQuestionId, int Order, string Type, List<string> SelectedOptions, string? AnswerText,
    List<string> CorrectOptions, bool? IsCorrect, decimal? Score, bool IsVoided);

public sealed record AdminEvent(string Type, DateTime ServerTime);

public sealed record AdminAttemptDetail(
    Guid AttemptId, string Status, string? SubmitReason, DateTime ExpiredAt, int TimeExtensionMinutes, string? CancelReason,
    decimal? TotalScore, int? GradingRevision, List<AdminAnswer> Answers, List<AdminEvent> Events);

public sealed record AdminAttemptRow(Guid AttemptId, string UserName, int AttemptNumber, string Status, decimal? TotalScore, int EventCount);

public sealed record AdminResultRow(Guid AttemptId, string UserName, int AttemptNumber, string Status, decimal TotalScore, bool IsOfficial);

public sealed record RegradeSummary(Guid CorrectionId, int AffectedAttempts, int ChangedResults);

public sealed record CorrectionRow(Guid Id, string CorrectionType, string Reason, int AffectedAttemptCount, string OldKeyJson, string NewKeyJson);

public sealed record Dashboard(int TotalUsers, int TotalStudents, int TotalExams, int OpenExams, int AttemptsToday, int InProgressAttempts);

public sealed record OptionStat(string OptionCode, int SelectedCount);

public sealed record QuestionStat(
    int Order, string Type, int AttemptCount, int AnsweredCount, int CorrectCount, int WrongCount, int BlankCount,
    decimal? CorrectRate, List<OptionStat> OptionDistribution);

public sealed record AuditRow(long Id, string Action, string? Reason, string? UserName, Guid? EntityId);

/// <summary>Vận hành admin (docs/02-nghiep-vu.md mục 8–9, docs/05-api.md mục 6.6, 6.8).</summary>
[Collection(ApiCollection.Name)]
public class AdminOperationsTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_sees_attempt_details_with_answers_and_events()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, userName) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<object>(
            $"/api/student/attempts/{attempt.AttemptId}/events", new { events = new[] { new { type = "WINDOW_BLUR" } } });
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (_, list) = await kit.Admin.GetJsonAsync<Paged<AdminAttemptRow>>($"/api/admin/exams/{exam.Id}/attempts");
        var (_, detail) = await kit.Admin.GetJsonAsync<AdminAttemptDetail>($"/api/admin/attempts/{attempt.AttemptId}");

        list.Data!.Items.Should().ContainSingle(a => a.UserName == userName && a.EventCount == 1 && a.TotalScore == 6m);
        detail.Data!.Answers.Should().HaveCount(5);
        detail.Data.Answers.First().CorrectOptions.Should().Equal("B");
        detail.Data.Answers.Should().OnlyContain(a => a.IsCorrect == true);
        detail.Data.Events.Should().ContainSingle(e => e.Type == "WINDOW_BLUR");
    }

    [Fact]
    public async Task Extend_force_submit_and_cancel_attempts()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 2, reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var url = $"/api/admin/attempts/{attempt.AttemptId}";

        var (noReason, _) = await kit.Admin.PostJsonAsync<object>($"{url}/extend", new { minutes = 10, reason = "" });
        var (extend, extended) = await kit.Admin.PostJsonAsync<AdminAttemptDetail>($"{url}/extend", new { minutes = 10, reason = "Mất điện" });
        var (force, forced) = await kit.Admin.PostJsonAsync<AdminAttemptDetail>($"{url}/force-submit", new { reason = "Vi phạm quy chế" });
        var (forceAgain, forceAgainBody) = await kit.Admin.PostJsonAsync<object>($"{url}/force-submit", new { reason = "Lần hai" });
        var (cancel, cancelled) = await kit.Admin.PostJsonAsync<AdminAttemptDetail>($"{url}/cancel", new { reason = "Thi lại do sự cố" });
        var (_, studentView) = await student.GetJsonAsync<StudentExamItem>($"/api/student/exams/{exam.Id}");

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        extend.StatusCode.Should().Be(HttpStatusCode.OK);
        extended.Data!.ExpiredAt.Should().Be(attempt.ExpiredAt.AddMinutes(10));
        extended.Data.TimeExtensionMinutes.Should().Be(10);
        force.StatusCode.Should().Be(HttpStatusCode.OK);
        forced.Data!.Status.Should().Be("AUTO_SUBMITTED");
        forced.Data.SubmitReason.Should().Be("FORCED_BY_ADMIN");
        forceAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        forceAgainBody.Errors.Single().Code.Should().Be("ATTEMPT_NOT_IN_PROGRESS");
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        cancelled.Data!.Status.Should().Be("CANCELLED");
        cancelled.Data.CancelReason.Should().Be("Thi lại do sự cố");
        studentView.Data!.UsedAttempts.Should().Be(0, "lượt bị hủy không tính vào số lượt đã dùng");

        var (_, audit) = await kit.Admin.GetJsonAsync<Paged<AuditRow>>($"/api/admin/audit-logs?action=ATTEMPT_EXTENDED&entityId={attempt.AttemptId}");
        audit.Data!.Items.Should().ContainSingle().Which.Reason.Should().Be("Mất điện");
    }

    [Fact]
    public async Task Correcting_answer_key_regrades_submitted_attempts_with_history()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var q1 = attempt.Questions.Single(q => q.Order == 1);
        await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "A" } }, q1.Id, 1)]);
        var (_, before) = await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");
        before.Data!.TotalScore.Should().Be(0m);

        var (_, version) = await kit.Admin.GetJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}");
        var examQuestionId = version.Data!.Questions.OrderBy(q => q.Order).First().Id;
        var keyUrl = $"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}/questions/{examQuestionId}";

        var (invalid, invalidBody) = await kit.Admin.PostJsonAsync<object>($"{keyUrl}/answer-key", new { correctOptionCodes = new[] { "A", "B" }, reason = "Hai đáp án" });
        var (correct, summary) = await kit.Admin.PostJsonAsync<RegradeSummary>($"{keyUrl}/answer-key", new { correctOptionCodes = new[] { "A" }, reason = "Đáp án gốc bị sai" });
        var (_, after) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");
        var (_, detail) = await kit.Admin.GetJsonAsync<AdminAttemptDetail>($"/api/admin/attempts/{attempt.AttemptId}");
        var (_, corrections) = await kit.Admin.GetJsonAsync<List<CorrectionRow>>($"/api/exams/{exam.Id}/answer-key-corrections");

        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "câu chọn một không được có 2 đáp án");
        invalidBody.Errors.Single().Code.Should().Be("INVALID_QUESTION");
        correct.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Data!.AffectedAttempts.Should().Be(1);
        summary.Data.ChangedResults.Should().Be(1);
        after.Data!.TotalScore.Should().Be(1m);
        detail.Data!.GradingRevision.Should().Be(2);
        detail.Data.Answers.First().SelectedOptions.Should().Equal(["A"], "câu trả lời của học viên không bị sửa");
        corrections.Data!.Should().ContainSingle(c => c.CorrectionType == "ANSWER_KEY" && c.AffectedAttemptCount == 1);
    }

    [Fact]
    public async Task Voiding_a_question_gives_everyone_full_score()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (_, version) = await kit.Admin.GetJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}");
        var multiple = version.Data!.Questions.OrderBy(q => q.Order).ElementAt(1);
        var (voided, _) = await kit.Admin.PostJsonAsync<RegradeSummary>(
            $"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}/questions/{multiple.Id}/void", new { reason = "Câu hỏi lỗi" });
        var (again, _) = await kit.Admin.PostJsonAsync<object>(
            $"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}/questions/{multiple.Id}/void", new { reason = "Lần hai" });
        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");

        voided.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        result.Data!.TotalScore.Should().Be(2m, "câu chọn nhiều (2 điểm) bị hủy nên ai cũng được trọn điểm");
    }

    [Fact]
    public async Task Results_list_marks_official_score_and_exports_xlsx()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 2, reviewPolicy: "NEVER");
        var (student, _, userName) = await kit.CreateStudentAsync();
        var first = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, first);
        await student.PostJsonAsync<object>($"/api/student/attempts/{first.AttemptId}/submit");
        var second = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<object>($"/api/student/attempts/{second.AttemptId}/submit");

        var (_, all) = await kit.Admin.GetJsonAsync<Paged<AdminResultRow>>($"/api/admin/exams/{exam.Id}/results");
        var (_, official) = await kit.Admin.GetJsonAsync<Paged<AdminResultRow>>($"/api/admin/exams/{exam.Id}/results?official=true");
        var export = await kit.Admin.GetAsync(new Uri($"/api/admin/exams/{exam.Id}/results/export", UriKind.Relative));
        var bytes = await export.Content.ReadAsByteArrayAsync();

        all.Data!.TotalCount.Should().Be(2);
        official.Data!.Items.Should().ContainSingle().Which.Should().Match<AdminResultRow>(
            r => r.UserName == userName && r.AttemptNumber == 1 && r.TotalScore == 6m && r.IsOfficial);
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        export.Content.Headers.ContentDisposition!.FileNameStar.Should().StartWith("ket-qua-");
        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
    }

    [Fact]
    public async Task Dashboard_and_question_statistics()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(reviewPolicy: "NEVER");
        var (s1, _, _) = await kit.CreateStudentAsync();
        var (s2, _, _) = await kit.CreateStudentAsync();
        var a1 = await AttemptTestKit.StartAsync(s1, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(s1, a1);
        await s1.PostJsonAsync<object>($"/api/student/attempts/{a1.AttemptId}/submit");
        var a2 = await AttemptTestKit.StartAsync(s2, exam.Id);
        var q1 = a2.Questions.Single(q => q.Order == 1);
        await AttemptTestKit.SaveAsync(s2, a2, [AttemptTestKit.Merge(new { selectedOptions = new[] { "A" } }, q1.Id, 1)]);
        await s2.PostJsonAsync<object>($"/api/student/attempts/{a2.AttemptId}/submit");

        var (dashboardResponse, dashboard) = await kit.Admin.GetJsonAsync<Dashboard>("/api/admin/dashboard");
        var (_, stats) = await kit.Admin.GetJsonAsync<List<QuestionStat>>($"/api/admin/reports/question-statistics?examId={exam.Id}");

        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        dashboard.Data!.TotalExams.Should().BeGreaterThan(0);
        dashboard.Data.AttemptsToday.Should().BeGreaterThanOrEqualTo(2);
        var first = stats.Data!.First();
        first.AttemptCount.Should().Be(2);
        first.CorrectCount.Should().Be(1);
        first.WrongCount.Should().Be(1);
        first.CorrectRate.Should().Be(50m);
        first.OptionDistribution.Should().Equal(new OptionStat("A", 1), new OptionStat("B", 1), new OptionStat("C", 0));
        stats.Data!.ElementAt(1).BlankCount.Should().Be(1, "học viên 2 bỏ trống câu 2");
    }

    [Fact]
    public async Task Student_cannot_use_admin_operations()
    {
        var student = factory.CreateHttpsClient();
        await student.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        (await student.GetJsonAsync<object>("/api/admin/dashboard")).Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetJsonAsync<object>("/api/admin/audit-logs")).Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

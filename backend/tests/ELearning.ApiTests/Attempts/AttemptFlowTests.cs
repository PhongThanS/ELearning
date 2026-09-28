using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.Application.Attempts;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.ApiTests.Attempts;

/// <summary>Lượt thi và chấm điểm (docs/02-nghiep-vu.md mục 6–7, docs/08-kiem-thu.md mục 4).</summary>
[Collection(ApiCollection.Name)]
public class AttemptFlowTests(ApiFactory factory)
{
    private static readonly string[] AnswerKeys =
        ["isCorrect", "acceptedAnswers", "correctAnswerNumber", "numericTolerance", "explanation", "correctOptions", "examQuestionId", "sourceQuestionId"];

    [Fact]
    public async Task Full_flow_start_save_submit_and_review()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();

        var (_, list) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");
        list.Data!.Items.Should().ContainSingle(e => e.ExamId == exam.Id).Which.Availability.Should().Be("AVAILABLE");

        var startResponse = await student.PostAsync(new Uri($"/api/student/exams/{exam.Id}/start", UriKind.Relative), null);
        var startJson = await startResponse.Content.ReadAsStringAsync();
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        AttemptTestKit.AllPropertyNames(startJson).Should().NotContain(AnswerKeys, "không lộ đáp án khi đang thi");
        startJson.Should().MatchRegex("\"expiredAt\":\"[^\"]+Z\"", "thời gian luôn có hậu tố Z");

        var attempt = (await student.GetJsonAsync<AttemptView>($"/api/student/attempts/{(await ReadAttemptId(startJson))}")).Body.Data!;
        attempt.Questions.Should().HaveCount(5);
        attempt.Questions.Select(q => q.Order).Should().Equal(1, 2, 3, 4, 5);

        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        var (_, reloaded) = await student.GetJsonAsync<AttemptView>($"/api/student/attempts/{attempt.AttemptId}");
        reloaded.Data!.Questions[1].Answer.SelectedOptions.Should().Equal("A", "C");
        reloaded.Data.Questions[4].Answer.AnswerText.Should().Be("3,5");

        var (submit, result) = await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Status.Should().Be("SUBMITTED");
        result.Data.ScoreVisible.Should().BeTrue();
        result.Data.TotalScore.Should().Be(6m);
        result.Data.MaxScore.Should().Be(6m);
        result.Data.Percentage.Should().Be(100m);
        result.Data.CorrectCount.Should().Be(5);
        result.Data.Passed.Should().BeTrue();
        result.Data.ReviewAvailable.Should().BeTrue("ReviewPolicy AFTER_SUBMIT với 1 lượt");
        result.Data.Questions!.First().CorrectOptions.Should().Equal("B");
        result.Data.Questions!.First().Explanation.Should().Be("B là đáp án đúng");
    }

    [Fact]
    public async Task Wrong_and_partial_answers_score_zero_per_question()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var q = attempt.Questions.OrderBy(x => x.Order).ToList();

        await AttemptTestKit.SaveAsync(student, attempt,
        [
            AttemptTestKit.Merge(new { selectedOptions = new[] { "B" } }, q[0].Id, 1),
            AttemptTestKit.Merge(new { selectedOptions = new[] { "A" } }, q[1].Id, 1),
            AttemptTestKit.Merge(new { answerText = "Hà Nội" }, q[3].Id, 1),
        ]);
        var (_, result) = await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        result.Data!.TotalScore.Should().Be(2m, "câu 1 (1đ) và câu 4 (1đ) đúng; câu 2 chọn thiếu → 0");
        result.Data.CorrectCount.Should().Be(2);
        result.Data.Percentage.Should().Be(33.33m);
        result.Data.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Start_twice_returns_the_same_attempt()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 2, reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();

        var first = await AttemptTestKit.StartAsync(student, exam.Id);
        var second = await AttemptTestKit.StartAsync(student, exam.Id, HttpStatusCode.OK);

        second.AttemptId.Should().Be(first.AttemptId);
        second.Resumed.Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_starts_create_exactly_one_attempt()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 3, reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => student.PostJsonAsync<AttemptView>($"/api/student/exams/{exam.Id}/start")));

        responses.Should().OnlyContain(r => r.Response.IsSuccessStatusCode);
        responses.Select(r => r.Body.Data!.AttemptId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Concurrent_submits_create_exactly_one_result()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit")));

        responses.Should().OnlyContain(r => r.Response.StatusCode == HttpStatusCode.OK);
        responses.Select(r => r.Body.Data!.TotalScore).Distinct().Should().Equal(6m);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ELearning.Infrastructure.Persistence.ELearningDbContext>();
        db.ExamResults.Count(r => r.AttemptId == attempt.AttemptId).Should().Be(1);
    }

    [Fact]
    public async Task Out_of_order_saves_keep_the_newest_answer()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var q1 = attempt.Questions.Single(q => q.Order == 1);

        var (_, newer) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "B" } }, q1.Id, 20, marked: true)]);
        var (_, older) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "A" } }, q1.Id, 19)]);
        var (_, current) = await student.GetJsonAsync<AttemptView>($"/api/student/attempts/{attempt.AttemptId}");

        newer.Data!.Answers.Single().Applied.Should().BeTrue();
        older.Data!.Answers.Single().Applied.Should().BeFalse();
        older.Data.Answers.Single().ClientSeq.Should().Be(20);
        var answer = current.Data!.Questions.Single(q => q.Order == 1).Answer;
        answer.SelectedOptions.Should().Equal("B");
        answer.IsMarkedForReview.Should().BeTrue("cờ đánh dấu xem lại được lưu trên server");
    }

    [Fact]
    public async Task Invalid_answer_shapes_are_rejected()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var q = attempt.Questions.OrderBy(x => x.Order).ToList();

        var (badOption, badOptionBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "Z" } }, q[0].Id, 1)]);
        var (twoForSingle, twoBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "A", "B" } }, q[0].Id, 1)]);
        var (badNumber, badNumberBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { answerText = "1,000.5" }, q[4].Id, 1)]);
        var (foreign, foreignBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { answerText = "x" }, Guid.NewGuid(), 1)]);

        badOption.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        badOptionBody.Errors.Single().Code.Should().Be("INVALID_OPTION");
        twoBody.Errors.Single().Code.Should().Be("INVALID_ANSWER_SHAPE");
        badNumberBody.Errors.Single().Code.Should().Be("INVALID_NUMBER_FORMAT");
        badNumberBody.Errors.Single().Field.Should().Be("answers[0].answerText");
        foreignBody.Errors.Single().Code.Should().Be("INVALID_ANSWER_SHAPE");
        twoForSingle.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        foreign.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Submitted_attempt_rejects_answers_and_resubmit_is_idempotent()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (save, saveBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "B" } }, attempt.Questions[0].Id, 5)]);
        var (again, againBody) = await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        save.StatusCode.Should().Be(HttpStatusCode.Conflict);
        saveBody.Errors.Single().Code.Should().Be("ATTEMPT_NOT_IN_PROGRESS");
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        againBody.Data!.TotalScore.Should().Be(0m);
    }

    [Fact]
    public async Task Max_attempts_are_enforced_and_extra_attempts_can_be_granted()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 1);
        var (student, userId, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (blocked, blockedBody) = await student.PostJsonAsync<object>($"/api/student/exams/{exam.Id}/start");
        await kit.Admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Id}/user-overrides/{userId}", new { extraAttempts = 1 });
        var second = await AttemptTestKit.StartAsync(student, exam.Id);

        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        blockedBody.Errors.Single().Code.Should().Be("MAX_ATTEMPTS_EXCEEDED");
        second.AttemptNumber.Should().Be(2);
    }

    [Fact]
    public async Task Expired_attempt_is_auto_submitted_when_saving_after_grace()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(durationMinutes: 1);
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var q1 = attempt.Questions.Single(q => q.Order == 1);

        factory.Time.Advance(TimeSpan.FromSeconds(60 + 29));
        var (withinGrace, _) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "B" } }, q1.Id, 1)]);
        factory.Time.Advance(TimeSpan.FromSeconds(2));
        var (late, lateBody) = await AttemptTestKit.SaveAsync(student, attempt, [AttemptTestKit.Merge(new { selectedOptions = new[] { "A" } }, q1.Id, 2)]);
        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");

        withinGrace.StatusCode.Should().Be(HttpStatusCode.OK, "trong 30 giây ân hạn vẫn được lưu");
        late.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        lateBody.Errors.Single().Code.Should().Be("ATTEMPT_EXPIRED");
        result.Data!.Status.Should().Be("AUTO_SUBMITTED");
        result.Data.SubmitReason.Should().Be("TIME_EXPIRED");
        result.Data.TotalScore.Should().Be(1m, "câu lưu trong ân hạn được chấm");
    }

    [Fact]
    public async Task Expiration_sweep_submits_abandoned_attempts()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(durationMinutes: 1);
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);

        factory.Time.Advance(TimeSpan.FromMinutes(2));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var processed = await scope.ServiceProvider.GetRequiredService<IAttemptExpirationService>().ProcessExpiredAsync(CancellationToken.None);
            processed.Should().BeGreaterThanOrEqualTo(1);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Chạy lại không chấm trùng
            await scope.ServiceProvider.GetRequiredService<IAttemptExpirationService>().ProcessExpiredAsync(CancellationToken.None);
        }

        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");
        result.Data!.Status.Should().Be("AUTO_SUBMITTED");
        result.Data.ScoreVisible.Should().BeTrue();
        result.Data.TotalScore.Should().Be(0m);
    }

    [Fact]
    public async Task End_at_caps_the_attempt_deadline()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var endAt = factory.Time.GetUtcNow().UtcDateTime.AddMinutes(5);
        var exam = await kit.CreatePublishedExamAsync(durationMinutes: 60, endAt: endAt);
        var (student, _, _) = await kit.CreateStudentAsync();

        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);

        attempt.ExpiredAt.Should().BeCloseTo(endAt, TimeSpan.FromMilliseconds(5), "D-05: min(StartedAt + thời lượng, EndAt)");
    }

    [Fact]
    public async Task Assigned_exam_is_invisible_to_other_students()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (allowed, allowedId, _) = await kit.CreateStudentAsync();
        var (other, _, _) = await kit.CreateStudentAsync();
        var exam = await kit.CreatePublishedExamAsync(accessMode: "ASSIGNED", assignUserIds: [allowedId]);

        var (_, otherList) = await other.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");
        var (otherGet, _) = await other.GetJsonAsync<object>($"/api/student/exams/{exam.Id}");
        var (otherStart, _) = await other.PostJsonAsync<object>($"/api/student/exams/{exam.Id}/start");
        var attempt = await AttemptTestKit.StartAsync(allowed, exam.Id);
        var (peek, _) = await other.GetJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}");

        otherList.Data!.Items.Should().NotContain(e => e.ExamId == exam.Id);
        otherGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherStart.StatusCode.Should().Be(HttpStatusCode.NotFound);
        peek.StatusCode.Should().Be(HttpStatusCode.NotFound, "lượt thi của người khác trả 404");
    }

    [Theory]
    [InlineData("NEVER", "IMMEDIATE", true, false)]
    [InlineData("AFTER_SUBMIT", "HIDDEN", false, false)]
    public async Task Result_visibility_policies_are_applied(string reviewPolicy, string scoreVisibility, bool scoreVisible, bool reviewAvailable)
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(reviewPolicy: reviewPolicy, scoreVisibility: scoreVisibility);
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);

        var submitResponse = await student.PostAsync(new Uri($"/api/student/attempts/{attempt.AttemptId}/submit", UriKind.Relative), null);
        var json = await submitResponse.Content.ReadAsStringAsync();
        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");

        result.Data!.ScoreVisible.Should().Be(scoreVisible);
        result.Data.ReviewAvailable.Should().Be(reviewAvailable);
        (result.Data.TotalScore is not null).Should().Be(scoreVisible);
        result.Data.Questions.Should().BeNull();
        json.Should().NotContain("correctOptions");
    }

    [Fact]
    public async Task Result_is_unchanged_after_bank_question_edit()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        await student.PostJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (_, version) = await kit.Admin.GetJsonAsync<Exams.VersionDetail>($"/api/exams/{exam.Id}/versions/{exam.PublishedVersionId}");
        var sourceId = version.Data!.Questions.OrderBy(q => q.Order).First().SourceQuestionId!.Value;
        var (_, source) = await kit.Admin.GetJsonAsync<Questions.QuestionDetail>($"/api/questions/{sourceId}");
        await kit.Admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/questions/{sourceId}", new
        {
            content = "Đã sửa",
            questionType = "SINGLE_CHOICE",
            options = new[] { new { content = "A", isCorrect = true }, new { content = "B", isCorrect = false } },
            rowVersion = source.Data!.RowVersion,
        });

        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");
        result.Data!.TotalScore.Should().Be(6m);
        result.Data.Questions!.First().CorrectOptions.Should().Equal(["B"], "xem lại bài vẫn dùng snapshot cũ");
    }

    [Fact]
    public async Task Closing_exam_with_force_submit_finishes_in_progress_attempts()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);

        await kit.Admin.PostJsonAsync<object>($"/api/exams/{exam.Id}/close", new { forceSubmitInProgress = true });
        var (_, result) = await student.GetJsonAsync<ResultView>($"/api/student/attempts/{attempt.AttemptId}/result");
        var (_, list) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");

        result.Data!.Status.Should().Be("AUTO_SUBMITTED");
        result.Data.SubmitReason.Should().Be("FORCED_BY_ADMIN");
        list.Data!.Items.Single(e => e.ExamId == exam.Id).Availability.Should().Be("CLOSED");
    }

    [Fact]
    public async Task Events_history_and_official_score()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(maxAttempts: 2, reviewPolicy: "NEVER");
        var (student, _, _) = await kit.CreateStudentAsync();
        var first = await AttemptTestKit.StartAsync(student, exam.Id);

        var (events, eventsBody) = await student.PostJsonAsync<RecordEventsResponse>(
            $"/api/student/attempts/{first.AttemptId}/events",
            new { events = new[] { new { type = "VISIBILITY_HIDDEN" }, new { type = "WINDOW_BLUR" } } });
        await AttemptTestKit.SaveAllCorrectAsync(student, first);
        await student.PostJsonAsync<ResultView>($"/api/student/attempts/{first.AttemptId}/submit");
        var second = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<ResultView>($"/api/student/attempts/{second.AttemptId}/submit");

        var (_, history) = await student.GetJsonAsync<Paged<ResultView>>("/api/student/history");
        var (_, list) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");

        events.StatusCode.Should().Be(HttpStatusCode.OK);
        eventsBody.Data!.Accepted.Should().Be(2);
        history.Data!.TotalCount.Should().Be(2);
        var item = list.Data!.Items.Single(e => e.ExamId == exam.Id);
        item.OfficialScore.Should().Be(6m, "RetakeScoringPolicy mặc định HIGHEST");
        item.UsedAttempts.Should().Be(2);
        item.Availability.Should().Be("NO_ATTEMPTS_LEFT");
    }

    [Fact]
    public async Task Admin_without_student_role_cannot_use_student_api()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);

        var (response, _) = await admin.GetJsonAsync<object>("/api/student/exams");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<Guid> ReadAttemptId(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.GetProperty("data").GetProperty("attemptId").GetGuid());
    }
}

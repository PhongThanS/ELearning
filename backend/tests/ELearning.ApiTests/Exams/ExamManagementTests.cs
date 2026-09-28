using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Questions;

namespace ELearning.ApiTests.Exams;

public sealed record VersionSummary(Guid Id, int VersionNumber, string Status, int QuestionCount, decimal MaxScore);

public sealed record ExamDetail(
    Guid Id, string Code, string Name, string Status, int MaxAttempts, string AccessMode,
    Guid? PublishedVersionId, Guid? DraftVersionId, int AssignmentCount, List<VersionSummary> Versions, string RowVersion);

public sealed record VersionOption(string OptionCode, string Content, bool IsCorrect, int DisplayOrder);

public sealed record VersionQuestion(
    Guid Id, int Order, Guid? SourceQuestionId, bool SourceChanged, string Content, string QuestionType, decimal Score,
    List<VersionOption> Options, decimal? CorrectAnswerNumber);

public sealed record VersionDetail(
    Guid Id, int VersionNumber, string Status, int DurationMinutes, string ReviewPolicy, int QuestionCount, decimal MaxScore,
    List<VersionQuestion> Questions, string RowVersion);

public sealed record PublishIssue(string Code, string Message, string? Field);

public sealed record PublishValidation(bool IsValid, List<PublishIssue> Issues);

/// <summary>Đề thi, version, snapshot, publish (docs/02-nghiep-vu.md mục 4, docs/05-api.md mục 6.4–6.5).</summary>
[Collection(ApiCollection.Name)]
public class ExamManagementTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_exam_starts_as_draft_with_version_one()
    {
        var admin = await AdminAsync();

        var exam = await CreateExamAsync(admin);
        var (duplicate, duplicateBody) = await admin.PostJsonAsync<object>("/api/exams", new { code = exam.Code, name = "Trùng" });

        exam.Status.Should().Be("DRAFT");
        exam.Versions.Should().ContainSingle().Which.Status.Should().Be("DRAFT");
        exam.DraftVersionId.Should().NotBeNull();
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicateBody.Errors.Single().Code.Should().Be("DUPLICATE_CODE");
    }

    [Fact]
    public async Task Publish_validation_returns_every_issue()
    {
        var admin = await AdminAsync();
        var exam = await CreateExamAsync(admin, maxAttempts: 3, reviewPolicy: "AFTER_SUBMIT", accessMode: "ASSIGNED");
        var versionId = exam.DraftVersionId!.Value;

        var (_, validation) = await admin.PostJsonAsync<PublishValidation>($"/api/exams/{exam.Id}/versions/{versionId}/validate");
        var (publish, publishBody) = await admin.PostJsonAsync<object>($"/api/exams/{exam.Id}/versions/{versionId}/publish");

        validation.Data!.IsValid.Should().BeFalse();
        validation.Data.Issues.Select(i => i.Code).Should().Contain(["NO_QUESTIONS", "REVIEW_AFTER_SUBMIT_WITH_RETAKES", "NO_ASSIGNMENTS"]);
        publish.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        publishBody.Errors.First().Code.Should().Be("PUBLISH_VALIDATION_FAILED");
        publishBody.Errors.Skip(1).Select(e => e.Code).Should().Contain("NO_QUESTIONS");
    }

    [Fact]
    public async Task Published_version_is_an_immutable_snapshot()
    {
        var admin = await AdminAsync();
        var question = await CreateNumberQuestionAsync(admin, answer: 4);
        var (exam, version) = await CreatePublishedExamAsync(admin, question.Id);

        // Sửa câu hỏi gốc sau khi publish không ảnh hưởng snapshot
        await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/questions/{question.Id}", new
        {
            content = "Đã sửa trong ngân hàng",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 99,
            rowVersion = question.RowVersion,
        });
        var (_, afterEdit) = await admin.GetJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{version.Id}");

        var (add, addBody) = await admin.PostJsonAsync<object>(
            $"/api/exams/{exam.Id}/versions/{version.Id}/questions", new { questionIds = new[] { question.Id } });
        var (settings, _) = await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Id}/versions/{version.Id}", new
        {
            durationMinutes = 10,
            scoreVisibility = "IMMEDIATE",
            reviewPolicy = "NEVER",
            rowVersion = version.RowVersion,
        });

        afterEdit.Data!.Status.Should().Be("PUBLISHED");
        afterEdit.Data.Questions.Single().Content.Should().Be("2 + 2 = ?");
        afterEdit.Data.Questions.Single().CorrectAnswerNumber.Should().Be(4);
        afterEdit.Data.Questions.Single().SourceChanged.Should().BeFalse("chỉ báo thay đổi cho bản nháp");
        add.StatusCode.Should().Be(HttpStatusCode.Conflict);
        addBody.Errors.Single().Code.Should().Be("VERSION_IMMUTABLE");
        settings.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task New_version_copies_published_one_detects_source_changes_and_archives_previous_on_publish()
    {
        var admin = await AdminAsync();
        var question = await CreateNumberQuestionAsync(admin, answer: 4);
        var (exam, v1) = await CreatePublishedExamAsync(admin, question.Id);

        var (created, v2) = await admin.PostJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions", new { });
        var (second, secondBody) = await admin.PostJsonAsync<object>($"/api/exams/{exam.Id}/versions", new { });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        v2.Data!.VersionNumber.Should().Be(2);
        v2.Data.Questions.Should().ContainSingle();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondBody.Errors.Single().Code.Should().Be("DRAFT_VERSION_EXISTS");

        await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/questions/{question.Id}", new
        {
            content = "2 + 3 = ?",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 5,
            rowVersion = question.RowVersion,
        });
        var (_, changed) = await admin.GetJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{v2.Data.Id}");
        changed.Data!.Questions.Single().SourceChanged.Should().BeTrue();

        var (_, synced) = await admin.PostJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{v2.Data.Id}/questions/sync", new { });
        synced.Data!.Questions.Single().Content.Should().Be("2 + 3 = ?");
        synced.Data.Questions.Single().SourceChanged.Should().BeFalse();

        var (publish, _) = await admin.PostJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{v2.Data.Id}/publish");
        var (_, detail) = await admin.GetJsonAsync<ExamDetail>($"/api/exams/{exam.Id}");
        var (_, oldVersion) = await admin.GetJsonAsync<VersionDetail>($"/api/exams/{exam.Id}/versions/{v1.Id}");

        publish.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Data!.PublishedVersionId.Should().Be(v2.Data.Id);
        oldVersion.Data!.Status.Should().Be("ARCHIVED");
        oldVersion.Data.Questions.Single().Content.Should().Be("2 + 2 = ?", "version cũ vẫn giữ nguyên snapshot");
    }

    [Fact]
    public async Task Draft_questions_can_be_reordered_rescored_and_removed()
    {
        var admin = await AdminAsync();
        var q1 = await CreateNumberQuestionAsync(admin, 1);
        var q2 = await CreateNumberQuestionAsync(admin, 2);
        var q3 = await CreateNumberQuestionAsync(admin, 3);
        var exam = await CreateExamAsync(admin);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";

        var (_, added) = await admin.PostJsonAsync<VersionDetail>(
            $"{versionUrl}/questions", new { questionIds = new[] { q1.Id, q2.Id, q3.Id, q1.Id } });
        var ids = added.Data!.Questions.Select(q => q.Id).ToList();
        var (reorder, reordered) = await admin.SendJsonAsync<VersionDetail>(
            HttpMethod.Put, $"{versionUrl}/questions/order", new { examQuestionIds = ids.AsEnumerable().Reverse() });
        var (badOrder, badOrderBody) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"{versionUrl}/questions/order", new { examQuestionIds = ids.Take(2) });
        var (_, rescored) = await admin.SendJsonAsync<VersionDetail>(
            HttpMethod.Patch, $"{versionUrl}/questions/{ids[0]}", new { score = 2.5 });
        var (_, removed) = await admin.SendJsonAsync<VersionDetail>(HttpMethod.Delete, $"{versionUrl}/questions/{ids[1]}");

        added.Data.Questions.Should().HaveCount(3, "thêm trùng câu hỏi nguồn bị bỏ qua");
        added.Data.Questions.Select(q => q.SourceQuestionId).Should().Equal(q1.Id, q2.Id, q3.Id);
        reorder.StatusCode.Should().Be(HttpStatusCode.OK);
        reordered.Data!.Questions.Select(q => q.SourceQuestionId).Should().Equal(q3.Id, q2.Id, q1.Id);
        badOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badOrderBody.Errors.Single().Code.Should().Be("ORDER_INVALID");
        rescored.Data!.MaxScore.Should().Be(4.5m);
        removed.Data!.Questions.Select(q => q.SourceQuestionId).Should().Equal(q3.Id, q1.Id);
    }

    [Fact]
    public async Task Preview_hides_correct_answers()
    {
        var admin = await AdminAsync();
        var question = await CreateNumberQuestionAsync(admin, 4);
        var exam = await CreateExamAsync(admin);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";
        await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { question.Id } });

        var response = await admin.GetAsync(new Uri($"{versionUrl}/preview", UriKind.Relative));
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContainAny("isCorrect", "correctAnswerNumber", "acceptedAnswers", "explanation", "numericTolerance");
    }

    [Fact]
    public async Task Close_reopen_and_delete_rules()
    {
        var admin = await AdminAsync();
        var draft = await CreateExamAsync(admin);
        var question = await CreateNumberQuestionAsync(admin, 4);
        var (published, _) = await CreatePublishedExamAsync(admin, question.Id);

        var (closeDraft, closeDraftBody) = await admin.PostJsonAsync<object>($"/api/exams/{draft.Id}/close", new { });
        var (close, closeBody) = await admin.PostJsonAsync<ExamDetail>($"/api/exams/{published.Id}/close", new { });
        var (reopen, reopenBody) = await admin.PostJsonAsync<ExamDetail>($"/api/exams/{published.Id}/reopen");
        var (deletePublished, deletePublishedBody) = await admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/exams/{published.Id}");
        var (deleteDraft, _) = await admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/exams/{draft.Id}");
        var (afterDelete, _) = await admin.GetJsonAsync<object>($"/api/exams/{draft.Id}");

        closeDraft.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        closeDraftBody.Errors.Single().Code.Should().Be("INVALID_STATE_TRANSITION");
        close.StatusCode.Should().Be(HttpStatusCode.OK);
        closeBody.Data!.Status.Should().Be("CLOSED");
        reopen.StatusCode.Should().Be(HttpStatusCode.OK);
        reopenBody.Data!.Status.Should().Be("PUBLISHED");
        deletePublished.StatusCode.Should().Be(HttpStatusCode.Conflict);
        deletePublishedBody.Errors.Single().Code.Should().Be("EXAM_NOT_DRAFT");
        deleteDraft.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Clone_creates_draft_copy_with_questions()
    {
        var admin = await AdminAsync();
        var question = await CreateNumberQuestionAsync(admin, 4);
        var (source, _) = await CreatePublishedExamAsync(admin, question.Id);

        var (response, clone) = await admin.PostJsonAsync<ExamDetail>($"/api/exams/{source.Id}/clone", new { });
        var (_, version) = await admin.GetJsonAsync<VersionDetail>($"/api/exams/{clone.Data!.Id}/versions/{clone.Data.DraftVersionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        clone.Data.Code.Should().Be($"{source.Code}-COPY");
        clone.Data.Status.Should().Be("DRAFT");
        version.Data!.Questions.Should().ContainSingle().Which.CorrectAnswerNumber.Should().Be(4);
    }

    [Fact]
    public async Task Updating_published_exam_enforces_cross_rules()
    {
        var admin = await AdminAsync();
        var question = await CreateNumberQuestionAsync(admin, 4);
        var (exam, _) = await CreatePublishedExamAsync(admin, question.Id, reviewPolicy: "AFTER_SUBMIT");

        var (codeChange, codeBody) = await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Id}", new
        {
            code = exam.Code + "X",
            name = exam.Name,
            maxAttempts = 1,
            accessMode = "PUBLIC",
            rowVersion = exam.RowVersion,
        });
        var (retakes, retakesBody) = await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Id}", new
        {
            name = exam.Name,
            maxAttempts = 2,
            accessMode = "PUBLIC",
            rowVersion = exam.RowVersion,
        });
        var (rename, renamed) = await admin.SendJsonAsync<ExamDetail>(HttpMethod.Put, $"/api/exams/{exam.Id}", new
        {
            name = "Tên mới",
            maxAttempts = 1,
            accessMode = "PUBLIC",
            rowVersion = exam.RowVersion,
        });

        codeChange.StatusCode.Should().Be(HttpStatusCode.Conflict);
        codeBody.Errors.Single().Code.Should().Be("EXAM_NOT_DRAFT");
        retakes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        retakesBody.Errors.Single().Code.Should().Be("REVIEW_AFTER_SUBMIT_WITH_RETAKES");
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        renamed.Data!.Name.Should().Be("Tên mới");
    }

    [Fact]
    public async Task Assignments_and_user_overrides()
    {
        var admin = await AdminAsync();
        var exam = await CreateExamAsync(admin, accessMode: "ASSIGNED");
        var (_, groups) = await admin.GetJsonAsync<Paged<GroupItem>>("/api/groups?keyword=DEMO");
        var demo = groups.Data!.Items.Single(g => g.Code == "DEMO");
        var (_, students) = await admin.GetJsonAsync<Paged<UserRow>>("/api/users?keyword=student02");
        var student = students.Data!.Items.Single();

        var (set, assignments) = await admin.SendJsonAsync<AssignmentsView>(
            HttpMethod.Put, $"/api/exams/{exam.Id}/assignments", new { groupIds = new[] { demo.Id }, userIds = new[] { student.Id } });
        var (grant, granted) = await admin.SendJsonAsync<OverrideView>(
            HttpMethod.Put, $"/api/exams/{exam.Id}/user-overrides/{student.Id}", new { extraAttempts = 2, note = "Mất điện" });
        var (badGrant, _) = await admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/exams/{exam.Id}/user-overrides/{student.Id}", new { extraAttempts = 99 });

        set.StatusCode.Should().Be(HttpStatusCode.OK);
        assignments.Data!.Groups.Should().ContainSingle(g => g.Code == "DEMO" && g.MemberCount == 2);
        assignments.Data.Users.Should().ContainSingle(u => u.UserName == "student02");
        grant.StatusCode.Should().Be(HttpStatusCode.OK);
        granted.Data!.ExtraAttempts.Should().Be(2);
        badGrant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Demo_exam_is_seeded_and_published()
    {
        var admin = await AdminAsync();

        var (_, list) = await admin.GetJsonAsync<Paged<ExamDetail>>("/api/exams?keyword=CS-BASIC&status=PUBLISHED");

        list.Data!.Items.Should().ContainSingle(e => e.Code == "CS-BASIC");
    }

    [Fact]
    public async Task Student_cannot_manage_exams()
    {
        var student = factory.CreateHttpsClient();
        await student.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (response, _) = await student.PostJsonAsync<object>("/api/exams", new { code = "HACK", name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public sealed record AssignmentsView(string AccessMode, List<AssignedGroup> Groups, List<AssignedUser> Users);

    public sealed record AssignedGroup(Guid Id, string Code, string Name, int MemberCount);

    public sealed record AssignedUser(Guid Id, string UserName, string FullName);

    public sealed record OverrideView(Guid ExamId, Guid UserId, int ExtraAttempts, string? Note);

    public sealed record UserRow(Guid Id, string UserName);

    internal static async Task<ExamDetail> CreateExamAsync(
        HttpClient admin, int maxAttempts = 1, string reviewPolicy = "NEVER", string accessMode = "PUBLIC")
    {
        var (response, body) = await admin.PostJsonAsync<ExamDetail>("/api/exams", new
        {
            code = ApiClientExtensions.UniqueName("EX-"),
            name = "Đề kiểm tra",
            maxAttempts,
            accessMode,
            durationMinutes = 30,
            passPercentage = 50,
            reviewPolicy,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }

    internal static async Task<QuestionDetail> CreateNumberQuestionAsync(HttpClient admin, decimal answer)
    {
        var (response, body) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = $"{answer - 2} + 2 = ?",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = answer,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }

    internal static async Task<(ExamDetail Exam, VersionDetail Version)> CreatePublishedExamAsync(
        HttpClient admin, Guid questionId, string reviewPolicy = "NEVER")
    {
        var exam = await CreateExamAsync(admin, reviewPolicy: reviewPolicy);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";
        await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { questionId } });
        var (publish, version) = await admin.PostJsonAsync<VersionDetail>($"{versionUrl}/publish");
        publish.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", version.Errors.Select(e => e.Code)));
        var (_, detail) = await admin.GetJsonAsync<ExamDetail>($"/api/exams/{exam.Id}");
        return (detail.Data!, version.Data!);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }
}

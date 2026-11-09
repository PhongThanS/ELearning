using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Questions;

namespace ELearning.ApiTests.Exams;

public sealed record ExamCategoryDetail(Guid Id, Guid? CategoryId, string? CategoryName, Guid? DraftVersionId, string RowVersion);

public sealed record SubCategoryItem(Guid Id, Guid? ParentId, string? ParentName, int ChildCount, int QuestionCount, int TotalQuestionCount);

public sealed record ExamCategoryListItem(Guid Id, Guid? CategoryId, string? CategoryName);

/// <summary>Chuyên đề của đề thi (D-30): mọi câu trong đề thuộc chuyên đề đã chọn; để trống thì không giới hạn.</summary>
[Collection(ApiCollection.Name)]
public class ExamCategoryTests(ApiFactory factory)
{
    [Fact]
    public async Task Exam_with_category_accepts_only_questions_of_that_category()
    {
        var admin = await AdminAsync();
        var math = await CreateCategoryAsync(admin);
        var physics = await CreateCategoryAsync(admin);
        var inMath = await CreateQuestionAsync(admin, math.Id);
        var inPhysics = await CreateQuestionAsync(admin, physics.Id);
        var noCategory = await CreateQuestionAsync(admin, null);

        var exam = await CreateExamAsync(admin, math.Id);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";
        var (other, otherBody) = await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { inMath.Id, inPhysics.Id } });
        var (none, _) = await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { noCategory.Id } });
        var (ok, okBody) = await admin.PostJsonAsync<VersionDetail>($"{versionUrl}/questions", new { questionIds = new[] { inMath.Id } });
        var (rule, ruleBody) = await admin.PostJsonAsync<object>($"{versionUrl}/pool-rules", new { categoryId = physics.Id, drawCount = 1, scorePerQuestion = 1 });
        var (_, list) = await admin.GetJsonAsync<Paged<ExamCategoryListItem>>($"/api/exams?categoryId={math.Id}");

        exam.CategoryId.Should().Be(math.Id);
        exam.CategoryName.Should().Be(math.Name);
        other.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "có câu thuộc chuyên đề khác");
        otherBody.Errors.Should().Contain(e => e.Code == "QUESTION_CATEGORY_MISMATCH");
        none.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "câu không có chuyên đề cũng không thuộc chuyên đề của đề");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        okBody.Data!.Questions.Should().ContainSingle(q => q.SourceQuestionId == inMath.Id);
        rule.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ruleBody.Errors.Should().Contain(e => e.Code == "QUESTION_CATEGORY_MISMATCH");
        list.Data!.Items.Should().ContainSingle(e => e.Id == exam.Id).Which.CategoryName.Should().Be(math.Name);
    }

    [Fact]
    public async Task Category_can_be_left_empty_and_changed_only_when_all_questions_match()
    {
        var admin = await AdminAsync();
        var math = await CreateCategoryAsync(admin);
        var physics = await CreateCategoryAsync(admin);
        var inMath = await CreateQuestionAsync(admin, math.Id);
        var inPhysics = await CreateQuestionAsync(admin, physics.Id);

        var exam = await CreateExamAsync(admin, null);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";
        var (mixed, _) = await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds = new[] { inMath.Id, inPhysics.Id } });
        var (toMath, toMathBody) = await UpdateAsync(admin, exam.Id, math.Id);

        var (_, version) = await admin.GetJsonAsync<VersionDetail>(versionUrl);
        var physicsQuestion = version.Data!.Questions.Single(q => q.SourceQuestionId == inPhysics.Id);
        await admin.SendJsonAsync<object>(HttpMethod.Delete, $"{versionUrl}/questions/{physicsQuestion.Id}");
        var (afterRemoval, afterRemovalBody) = await UpdateAsync(admin, exam.Id, math.Id);
        var (cleared, clearedBody) = await UpdateAsync(admin, exam.Id, null);

        exam.CategoryId.Should().BeNull();
        mixed.StatusCode.Should().Be(HttpStatusCode.OK, "đề không có chuyên đề nhận câu của mọi chuyên đề");
        toMath.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        toMathBody.Errors.Should().Contain(e => e.Code == "EXAM_CATEGORY_MISMATCH");
        afterRemoval.StatusCode.Should().Be(HttpStatusCode.OK);
        afterRemovalBody.Data!.CategoryId.Should().Be(math.Id);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        clearedBody.Data!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task Parent_category_includes_questions_of_its_subcategories()
    {
        var admin = await AdminAsync();
        var parent = await CreateCategoryAsync(admin);
        var child = await CreateCategoryAsync(admin, parent.Id);
        var child2 = await CreateCategoryAsync(admin, parent.Id);
        var leaf = await CreateCategoryAsync(admin);
        var inChild2 = await CreateQuestionAsync(admin, child2.Id);
        var inChild = await CreateQuestionAsync(admin, child.Id);
        await CreateQuestionAsync(admin, leaf.Id);

        // Chuyên đề có con chỉ gom nhóm: không thêm câu trực tiếp; chuyên đề đang có câu không nhận chuyên đề con
        var (inParent, inParentBody) = await admin.PostJsonAsync<object>("/api/questions", new
        {
            content = "1 + 1 = ?",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 2,
            categoryId = parent.Id,
        });
        var (childOfLeaf, childOfLeafBody) = await admin.PostJsonAsync<object>(
            "/api/question-categories", new { code = ApiClientExtensions.UniqueName("CD"), name = "Con", parentId = leaf.Id });
        inParent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        inParentBody.Errors.Should().Contain(e => e.Code == "CATEGORY_HAS_CHILDREN");
        childOfLeaf.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        childOfLeafBody.Errors.Should().Contain(e => e.Code == "PARENT_HAS_QUESTIONS");

        var (grandChild, grandChildBody) = await admin.PostJsonAsync<object>(
            "/api/question-categories", new { code = ApiClientExtensions.UniqueName("CD"), name = "Cháu", parentId = child.Id });
        var (_, parentInfo) = await admin.GetJsonAsync<SubCategoryItem>($"/api/question-categories/{parent.Id}");
        var (_, childInfo) = await admin.GetJsonAsync<SubCategoryItem>($"/api/question-categories/{child.Id}");
        var (_, questions) = await admin.GetJsonAsync<Paged<QuestionDetail>>($"/api/questions?categoryId={parent.Id}");
        var (_, childQuestions) = await admin.GetJsonAsync<Paged<QuestionDetail>>($"/api/questions?categoryId={child.Id}");

        var exam = await CreateExamAsync(admin, parent.Id);
        var versionUrl = $"/api/exams/{exam.Id}/versions/{exam.DraftVersionId}";
        var (add, addBody) = await admin.PostJsonAsync<VersionDetail>($"{versionUrl}/questions", new { questionIds = new[] { inChild2.Id, inChild.Id } });
        var (rule, ruleBody) = await admin.PostJsonAsync<object>($"{versionUrl}/pool-rules", new { categoryId = leaf.Id, drawCount = 1, scorePerQuestion = 1 });
        var (deleteParent, deleteParentBody) = await admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/question-categories/{parent.Id}?force=true");

        grandChild.StatusCode.Should().Be(HttpStatusCode.BadRequest, "chỉ có 2 cấp");
        grandChildBody.Errors.Should().Contain(e => e.Code == "PARENT_INVALID");
        parentInfo.Data!.Should().Match<SubCategoryItem>(c => c.ParentId == null && c.ChildCount == 2 && c.QuestionCount == 0 && c.TotalQuestionCount == 2);
        childInfo.Data!.Should().Match<SubCategoryItem>(c => c.ParentId == parent.Id && c.ParentName == parent.Name && c.ChildCount == 0);
        questions.Data!.TotalCount.Should().Be(2, "lọc chuyên đề cha gồm cả câu của chuyên đề con");
        childQuestions.Data!.TotalCount.Should().Be(1);
        add.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", addBody.Errors.Select(e => e.Code)));
        addBody.Data!.Questions.Should().HaveCount(2, "đề thuộc chuyên đề cha nhận câu của chuyên đề con");
        rule.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ruleBody.Errors.Should().Contain(e => e.Code == "QUESTION_CATEGORY_MISMATCH");
        deleteParent.StatusCode.Should().Be(HttpStatusCode.Conflict);
        deleteParentBody.Errors.Should().Contain(e => e.Code == "CATEGORY_HAS_CHILDREN");
    }

    private static async Task<(HttpResponseMessage, Envelope<ExamCategoryDetail>)> UpdateAsync(HttpClient admin, Guid examId, Guid? categoryId)
    {
        var (_, current) = await admin.GetJsonAsync<ExamCategoryDetail>($"/api/exams/{examId}");
        return await admin.SendJsonAsync<ExamCategoryDetail>(HttpMethod.Put, $"/api/exams/{examId}", new
        {
            name = "Đề theo chuyên đề",
            maxAttempts = 1,
            accessMode = "PUBLIC",
            categoryId,
            rowVersion = current.Data!.RowVersion,
        });
    }

    private static async Task<ExamCategoryDetail> CreateExamAsync(HttpClient admin, Guid? categoryId)
    {
        var (response, body) = await admin.PostJsonAsync<ExamCategoryDetail>("/api/exams", new
        {
            code = ApiClientExtensions.UniqueName("EX-"),
            name = "Đề theo chuyên đề",
            accessMode = "PUBLIC",
            durationMinutes = 30,
            categoryId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }

    private static async Task<CategoryItem> CreateCategoryAsync(HttpClient admin, Guid? parentId = null)
    {
        var code = ApiClientExtensions.UniqueName("CD");
        var (response, body) = await admin.PostJsonAsync<CategoryItem>("/api/question-categories", new { code, name = $"Chuyên đề {code}", parentId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }

    private static async Task<QuestionDetail> CreateQuestionAsync(HttpClient admin, Guid? categoryId)
    {
        var (response, body) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "1 + 1 = ?",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 2,
            categoryId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }
}

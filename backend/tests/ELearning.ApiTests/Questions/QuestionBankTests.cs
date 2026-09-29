using System.Net;
using ELearning.ApiTests.Admin;

namespace ELearning.ApiTests.Questions;

public sealed record CategoryItem(Guid Id, string Code, string Name, bool IsActive, int QuestionCount, string RowVersion);

public sealed record OptionItem(Guid Id, string OptionCode, string Content, bool IsCorrect, int DisplayOrder);

public sealed record QuestionDetail(
    Guid Id,
    string Code,
    Guid? CategoryId,
    string Content,
    string ContentFormat,
    string QuestionType,
    string? AnswerDataType,
    decimal? CorrectAnswerNumber,
    decimal? NumericTolerance,
    bool IgnoreAccent,
    decimal DefaultScore,
    bool IsActive,
    List<OptionItem> Options,
    List<string> AcceptedAnswers,
    string RowVersion);

public sealed record QuestionListItem(Guid Id, string Code, string ContentPreview, string QuestionType, bool IsActive);

/// <summary>Ngân hàng câu hỏi (docs/02-nghiep-vu.md mục 1, docs/05-api.md mục 6.3).</summary>
[Collection(ApiCollection.Name)]
public class QuestionBankTests(ApiFactory factory)
{
    [Fact]
    public async Task Creates_all_four_question_types()
    {
        var admin = await AdminAsync();

        var (single, singleBody) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "Thủ đô Việt Nam là?",
            questionType = "SINGLE_CHOICE",
            options = new[]
            {
                new { content = "Hà Nội", isCorrect = true },
                new { content = "Huế", isCorrect = false },
                new { content = "Đà Nẵng", isCorrect = false },
            },
        });
        var (multiple, _) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "Chọn các số chẵn",
            questionType = "MULTIPLE_CHOICE",
            defaultScore = 2,
            options = new[]
            {
                new { optionCode = "A", content = "2", isCorrect = true },
                new { optionCode = "B", content = "3", isCorrect = false },
                new { optionCode = "C", content = "4", isCorrect = true },
            },
        });
        var (trueFalse, trueFalseBody) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "Trái Đất quay quanh Mặt Trời.",
            questionType = "TRUE_FALSE",
            options = new[]
            {
                new { optionCode = "FALSE", content = "", isCorrect = false },
                new { optionCode = "TRUE", content = "", isCorrect = true },
            },
        });
        var (text, textBody) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "Thủ đô Việt Nam?",
            questionType = "FILL_IN",
            answerDataType = "TEXT",
            acceptedAnswers = new[] { "Hà Nội", "Thành phố Hà Nội" },
            ignoreAccent = true,
        });
        var (number, numberBody) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "2 + 2 = ?",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 4,
        });

        single.StatusCode.Should().Be(HttpStatusCode.Created);
        singleBody.Data!.Code.Should().MatchRegex("^Q[0-9]{6}$", "mã tự sinh khi để trống");
        singleBody.Data.Options.Select(o => o.OptionCode).Should().Equal("A", "B", "C");
        multiple.StatusCode.Should().Be(HttpStatusCode.Created);
        trueFalse.StatusCode.Should().Be(HttpStatusCode.Created);
        trueFalseBody.Data!.Options.Select(o => (o.OptionCode, o.Content)).Should().Equal(("TRUE", "Đúng"), ("FALSE", "Sai"));
        text.StatusCode.Should().Be(HttpStatusCode.Created);
        textBody.Data!.AcceptedAnswers.Should().Equal("Hà Nội", "Thành phố Hà Nội");
        textBody.Data.Options.Should().BeEmpty();
        number.StatusCode.Should().Be(HttpStatusCode.Created);
        numberBody.Data!.CorrectAnswerNumber.Should().Be(4);
        numberBody.Data.NumericTolerance.Should().Be(0, "sai số mặc định 0");
    }

    [Theory]
    [MemberData(nameof(InvalidQuestions))]
    public async Task Rejects_invalid_questions_with_specific_error_codes(object body, string expectedCode)
    {
        var admin = await AdminAsync();

        var (response, envelope) = await admin.PostJsonAsync<object>("/api/questions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        envelope.Errors.Select(e => e.Code).Should().Contain(expectedCode);
    }

    public static TheoryData<object, string> InvalidQuestions() => new()
    {
        {
            new
            {
                content = "Hai đáp án đúng",
                questionType = "SINGLE_CHOICE",
                options = new[] { new { content = "A", isCorrect = true }, new { content = "B", isCorrect = true } },
            },
            "SINGLE_CHOICE_REQUIRES_ONE_CORRECT"
        },
        {
            new
            {
                content = "Không có đáp án đúng",
                questionType = "MULTIPLE_CHOICE",
                options = new[] { new { content = "A", isCorrect = false }, new { content = "B", isCorrect = false } },
            },
            "MULTIPLE_CHOICE_REQUIRES_CORRECT"
        },
        {
            new { content = "Một lựa chọn", questionType = "SINGLE_CHOICE", options = new[] { new { content = "A", isCorrect = true } } },
            "OPTIONS_COUNT_INVALID"
        },
        {
            new
            {
                content = "Sai mã",
                questionType = "TRUE_FALSE",
                options = new[] { new { optionCode = "A", content = "Đúng", isCorrect = true }, new { optionCode = "B", content = "Sai", isCorrect = false } },
            },
            "TRUE_FALSE_INVALID"
        },
        { new { content = "Điền", questionType = "FILL_IN", answerDataType = "TEXT", acceptedAnswers = Array.Empty<string>() }, "ACCEPTED_ANSWERS_COUNT_INVALID" },
        { new { content = "Điền", questionType = "FILL_IN", answerDataType = "TEXT", acceptedAnswers = new[] { "Hà Nội", " hà  nội " } }, "ACCEPTED_ANSWER_DUPLICATE" },
        { new { content = "Số", questionType = "FILL_IN", answerDataType = "NUMBER" }, "CORRECT_NUMBER_REQUIRED" },
        { new { content = "Số", questionType = "FILL_IN", answerDataType = "NUMBER", correctAnswerNumber = 1, numericTolerance = -1 }, "TOLERANCE_NEGATIVE" },
        { new { content = "Điền", questionType = "FILL_IN" }, "ANSWER_DATA_TYPE_REQUIRED" },
        { new { content = "", questionType = "FILL_IN", answerDataType = "NUMBER", correctAnswerNumber = 1 }, "CONTENT_REQUIRED" },
        { new { content = "Điểm lẻ", questionType = "FILL_IN", answerDataType = "NUMBER", correctAnswerNumber = 1, defaultScore = 0.3 }, "SCORE_STEP_INVALID" },
    };

    [Fact]
    public async Task Unknown_question_type_is_rejected()
    {
        var admin = await AdminAsync();

        var (response, _) = await admin.PostJsonAsync<object>("/api/questions", new { content = "X", questionType = "MATCHING" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_code_is_rejected()
    {
        var admin = await AdminAsync();
        var code = ApiClientExtensions.UniqueName("DUP-");
        var body = new { code, content = "2 + 2", questionType = "FILL_IN", answerDataType = "NUMBER", correctAnswerNumber = 4 };

        var (first, _) = await admin.PostJsonAsync<object>("/api/questions", body);
        var (second, secondBody) = await admin.PostJsonAsync<object>("/api/questions", body);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondBody.Errors.Single().Code.Should().Be("DUPLICATE_CODE");
    }

    [Fact]
    public async Task Update_replaces_options_and_detects_stale_row_version()
    {
        var admin = await AdminAsync();
        var created = await CreateSingleChoiceAsync(admin);

        var update = new
        {
            content = "Câu hỏi đã sửa",
            questionType = "MULTIPLE_CHOICE",
            options = new[]
            {
                new { optionCode = "A", content = "Mới A", isCorrect = true },
                new { optionCode = "B", content = "Mới B", isCorrect = true },
                new { optionCode = "C", content = "Mới C", isCorrect = false },
                new { optionCode = "D", content = "Mới D", isCorrect = false },
            },
            rowVersion = created.RowVersion,
        };
        var (updated, updatedBody) = await admin.SendJsonAsync<QuestionDetail>(HttpMethod.Put, $"/api/questions/{created.Id}", update);
        var (stale, staleBody) = await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/questions/{created.Id}", update);

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedBody.Data!.QuestionType.Should().Be("MULTIPLE_CHOICE");
        updatedBody.Data.Options.Select(o => o.Content).Should().Equal("Mới A", "Mới B", "Mới C", "Mới D");
        updatedBody.Data.Code.Should().Be(created.Code, "mã không đổi khi sửa");
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        staleBody.Errors.Single().Code.Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Status_toggle_and_clone()
    {
        var admin = await AdminAsync();
        var created = await CreateSingleChoiceAsync(admin);

        var (off, offBody) = await admin.SendJsonAsync<QuestionDetail>(
            HttpMethod.Patch, $"/api/questions/{created.Id}/status", new { isActive = false });
        var (clone, cloneBody) = await admin.PostJsonAsync<QuestionDetail>($"/api/questions/{created.Id}/clone");
        var (delete, _) = await admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/questions/{created.Id}");

        off.StatusCode.Should().Be(HttpStatusCode.OK);
        offBody.Data!.IsActive.Should().BeFalse();
        clone.StatusCode.Should().Be(HttpStatusCode.Created);
        cloneBody.Data!.Code.Should().NotBe(created.Code);
        cloneBody.Data.IsActive.Should().BeTrue();
        cloneBody.Data.Options.Select(o => (o.OptionCode, o.Content, o.IsCorrect))
            .Should().Equal(created.Options.Select(o => (o.OptionCode, o.Content, o.IsCorrect)));
        delete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed, "không có xóa cứng câu hỏi (D-16)");
    }

    [Fact]
    public async Task List_filters_by_keyword_type_and_category()
    {
        var admin = await AdminAsync();
        var (_, category) = await admin.PostJsonAsync<CategoryItem>(
            "/api/question-categories", new { code = ApiClientExtensions.UniqueName("CAT"), name = "Danh mục lọc" });
        var marker = ApiClientExtensions.UniqueName("tukhoa");
        await admin.PostJsonAsync<object>("/api/questions", new
        {
            categoryId = category.Data!.Id,
            content = $"Câu có {marker} và ký tự 50%",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 1,
        });

        var (byKeyword, byKeywordBody) = await admin.GetJsonAsync<Paged<QuestionListItem>>($"/api/questions?keyword={marker}");
        var (byCategory, byCategoryBody) = await admin.GetJsonAsync<Paged<QuestionListItem>>(
            $"/api/questions?categoryId={category.Data.Id}&questionType=FILL_IN");
        var (wrongType, wrongTypeBody) = await admin.GetJsonAsync<Paged<QuestionListItem>>(
            $"/api/questions?categoryId={category.Data.Id}&questionType=SINGLE_CHOICE");
        var (wildcard, wildcardBody) = await admin.GetJsonAsync<Paged<QuestionListItem>>("/api/questions?keyword=50%25");

        byKeyword.StatusCode.Should().Be(HttpStatusCode.OK);
        byKeywordBody.Data!.TotalCount.Should().Be(1);
        byCategoryBody.Data!.TotalCount.Should().Be(1);
        wrongTypeBody.Data!.TotalCount.Should().Be(0);
        wildcardBody.Data!.Items.Should().OnlyContain(q => q.ContentPreview.Contains("50%"), "% trong từ khóa được escape");
        byCategory.StatusCode.Should().Be(HttpStatusCode.OK);
        wrongType.StatusCode.Should().Be(HttpStatusCode.OK);
        wildcard.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inactive_category_cannot_receive_new_questions()
    {
        var admin = await AdminAsync();
        var (_, category) = await admin.PostJsonAsync<CategoryItem>(
            "/api/question-categories", new { code = ApiClientExtensions.UniqueName("OFF"), name = "Sẽ tắt" });
        await admin.SendJsonAsync<CategoryItem>(HttpMethod.Patch, $"/api/question-categories/{category.Data!.Id}/status", new { isActive = false });

        var (response, body) = await admin.PostJsonAsync<object>("/api/questions", new
        {
            categoryId = category.Data.Id,
            content = "X",
            questionType = "FILL_IN",
            answerDataType = "NUMBER",
            correctAnswerNumber = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Errors.Single().Field.Should().Be("categoryId");
    }

    [Fact]
    public async Task Category_crud_with_row_version()
    {
        var admin = await AdminAsync();
        var code = ApiClientExtensions.UniqueName("C#");

        var (created, createdBody) = await admin.PostJsonAsync<CategoryItem>("/api/question-categories", new { code, name = "Lập trình" });
        var (duplicate, _) = await admin.PostJsonAsync<object>("/api/question-categories", new { code, name = "Trùng" });
        var (updated, updatedBody) = await admin.SendJsonAsync<CategoryItem>(
            HttpMethod.Put, $"/api/question-categories/{createdBody.Data!.Id}",
            new { name = "Lập trình C#", isActive = true, rowVersion = createdBody.Data.RowVersion });
        var (list, listBody) = await admin.GetJsonAsync<Paged<CategoryItem>>("/api/question-categories?pageSize=100");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedBody.Data!.Name.Should().Be("Lập trình C#");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        listBody.Data!.Items.Should().Contain(c => c.Code == "CSHARP" && c.QuestionCount >= 10, "dữ liệu demo đã được seed");
    }

    [Fact]
    public async Task Student_cannot_read_question_bank()
    {
        var student = factory.CreateHttpsClient();
        await student.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (response, _) = await student.GetJsonAsync<object>("/api/questions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }

    private static async Task<QuestionDetail> CreateSingleChoiceAsync(HttpClient admin)
    {
        var (response, body) = await admin.PostJsonAsync<QuestionDetail>("/api/questions", new
        {
            content = "Câu chọn một",
            questionType = "SINGLE_CHOICE",
            options = new[]
            {
                new { content = "Đúng", isCorrect = true },
                new { content = "Sai 1", isCorrect = false },
                new { content = "Sai 2", isCorrect = false },
            },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return body.Data!;
    }
}

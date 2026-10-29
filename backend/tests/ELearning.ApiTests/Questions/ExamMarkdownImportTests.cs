using System.Net;
using ELearning.ApiTests.Admin;

namespace ELearning.ApiTests.Questions;

public sealed record MdIssue(int? Line, string? Field, string Code, string Message);

public sealed record MdQuestion(int Index, string Label, Guid? QuestionId, string? Code, List<MdIssue> Issues);

public sealed record MdResult(
    bool DryRun, bool Imported, Guid? CategoryId, int TotalQuestions, int ValidQuestions, decimal TotalScore,
    List<MdQuestion> Questions, List<MdIssue> FileIssues, List<string> MissingImages);

public sealed record MdQuestionDetail(Guid Id, string Code, string QuestionType, string Content, string? Explanation);

/// <summary>Nhập đề từ file Markdown "elearning-exam/1" (docs/02-nghiep-vu.md mục 1.5, D-29).</summary>
[Collection(ApiCollection.Name)]
public class ExamMarkdownImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Dry_run_previews_then_import_creates_all_questions_in_category()
    {
        var admin = await AdminAsync();
        var category = await CreateCategoryAsync(admin);
        var markdown = Exam(category.Code);

        var (preview, previewBody) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown, dryRun = true });
        var (_, before) = await admin.GetJsonAsync<Paged<object>>($"/api/questions?categoryId={category.Id}");
        var (imported, importedBody) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown, dryRun = false });
        var (_, after) = await admin.GetJsonAsync<Paged<object>>($"/api/questions?categoryId={category.Id}");

        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        previewBody.Data!.Imported.Should().BeFalse();
        previewBody.Data.TotalQuestions.Should().Be(3);
        previewBody.Data.ValidQuestions.Should().Be(3);
        previewBody.Data.TotalScore.Should().Be(1.75m);
        previewBody.Data.CategoryId.Should().Be(category.Id, "@category trong file trỏ tới danh mục");
        before.Data!.TotalCount.Should().Be(0, "kiểm tra không tạo câu hỏi");

        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        importedBody.Data!.Imported.Should().BeTrue();
        importedBody.Data.Questions.Should().OnlyContain(q => q.QuestionId != null && q.Code != null);
        after.Data!.TotalCount.Should().Be(3);

        var tf = importedBody.Data.Questions[1];
        var (_, detail) = await admin.GetJsonAsync<MdQuestionDetail>($"/api/questions/{tf.QuestionId}");
        detail.Data!.QuestionType.Should().Be("TRUE_FALSE");
        detail.Data.Content.Should().Contain("$y = x^3$");
    }

    [Fact]
    public async Task Import_is_all_or_nothing_and_requires_uploaded_images()
    {
        var admin = await AdminAsync();
        var category = await CreateCategoryAsync(admin);
        var withImage = Exam(category.Code).Replace("@content: Cho hàm số $y = x^3$.", "@content: Cho hình ![Hình 1](hinh-1.png)");
        var pending = Exam(category.Code).Replace("@content: Cho hàm số $y = x^3$.", "@content: Cho hình ![](media:pending-abc123)");
        var broken = Exam(category.Code).Replace("@answer: 3", "@answer: ba");

        var (_, missing) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown = withImage, dryRun = false });
        var (_, notUploaded) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown = pending, dryRun = false });
        var (_, pendingPreview) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown = pending, dryRun = true });
        var (_, invalid) = await admin.PostJsonAsync<MdResult>("/api/questions/import-markdown", new { markdown = broken, dryRun = false });
        var (_, list) = await admin.GetJsonAsync<Paged<object>>($"/api/questions?categoryId={category.Id}");

        missing.Data!.Imported.Should().BeFalse();
        missing.Data.MissingImages.Should().Equal("hinh-1.png");
        notUploaded.Data!.Imported.Should().BeFalse();
        notUploaded.Data.Questions[1].Issues.Should().Contain(i => i.Code == "IMAGE_NOT_UPLOADED");
        pendingPreview.Data!.ValidQuestions.Should().Be(3, "ảnh chờ tải được phép khi chỉ kiểm tra");
        invalid.Data!.Imported.Should().BeFalse();
        invalid.Data.Questions[2].Issues.Should().Contain(i => i.Code == "ANSWER_INVALID" && i.Line != null);
        list.Data!.TotalCount.Should().Be(0, "có lỗi thì không câu nào được tạo");
    }

    [Fact]
    public async Task Shipped_sample_and_prompt_example_pass_full_validation()
    {
        var admin = await AdminAsync();
        var category = await CreateCategoryAsync(admin);
        const string fence = "```";
        var prompt = File.ReadAllText(PublicFile("prompt-chuyen-de-thi.md")).ReplaceLineEndings("\n");
        var start = prompt.IndexOf(fence + "markdown\n@format:", StringComparison.Ordinal) + fence.Length + "markdown\n".Length;
        var example = prompt[start..prompt.IndexOf("\n" + fence + "\n", start, StringComparison.Ordinal)];

        foreach (var markdown in new[] { File.ReadAllText(PublicFile("mau-de-thi.md")), example })
        {
            var (_, body) = await admin.PostJsonAsync<MdResult>(
                "/api/questions/import-markdown", new { markdown, categoryId = category.Id, dryRun = true });

            body.Data!.FileIssues.Should().BeEmpty();
            body.Data.Questions.SelectMany(q => q.Issues).Should().BeEmpty("file mẫu và ví dụ trong prompt phải qua đúng bộ kiểm tra khi tạo câu hỏi");
            body.Data.ValidQuestions.Should().Be(body.Data.TotalQuestions);
        }
    }

    [Fact]
    public async Task Student_cannot_import()
    {
        var student = factory.CreateHttpsClient();
        var userName = ApiClientExtensions.UniqueName("md");
        await student.PostJsonAsync<object>("/api/auth/register", new
        {
            userName,
            email = $"{userName}@test.local",
            fullName = "Học viên",
            password = "Student#2026",
            acceptTerms = true,
        }, csrf: true);
        await student.LoginAsync(userName, "Student#2026");

        var (response, _) = await student.PostJsonAsync<object>("/api/questions/import-markdown", new { markdown = Exam("X"), dryRun = true });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    private static string Exam(string categoryCode) => """
        @format: elearning-exam/1
        @title: Đề kiểm tra
        @category: CATEGORY_CODE

        @question: 1
        @type: SINGLE_CHOICE
        @score: 0.25
        @content: Một cộng một bằng
        @option A: $1$
        @option B: $2$
        @answer: B

        @question: 2
        @type: TRUE_FALSE
        @score: 1
        @content: Cho hàm số $y = x^3$.
        @statement a: Đồng biến trên $\mathbb{R}$.
        @statement b: Có cực trị.
        @answer: a=Đ, b=S

        @question: 3
        @type: NUMBER
        @score: 0.5
        @content: $1 + 2 = ?$
        @answer: 3
        """.Replace("CATEGORY_CODE", categoryCode, StringComparison.Ordinal);

    private static async Task<CategoryItem> CreateCategoryAsync(HttpClient admin)
    {
        var (_, body) = await admin.PostJsonAsync<CategoryItem>(
            "/api/question-categories", new { code = ApiClientExtensions.UniqueName("MD"), name = "Nhập đề Markdown" });
        return body.Data!;
    }

    private static string PublicFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "elearning-web", "public", "huong-dan", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Không tìm thấy frontend/elearning-web/public/huong-dan/{name}");
    }

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return admin;
    }
}

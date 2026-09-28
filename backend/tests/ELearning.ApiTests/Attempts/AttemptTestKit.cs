using System.Net;
using System.Text.Json;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Exams;

namespace ELearning.ApiTests.Attempts;

public sealed record PlayerOption(string Code, string Content);

public sealed record AnswerState(List<string> SelectedOptions, string? AnswerText, bool IsMarkedForReview, long ClientSeq);

public sealed record AttemptQuestionView(Guid Id, int Order, string Content, string Type, string? AnswerDataType, decimal Score, List<PlayerOption> Options, AnswerState Answer);

public sealed record AttemptView(
    Guid AttemptId, Guid ExamId, string ExamName, int AttemptNumber, string Status, bool Resumed,
    DateTime StartedAt, DateTime ExpiredAt, DateTime ServerTime, List<AttemptQuestionView> Questions);

public sealed record SavedAnswer(Guid QuestionId, bool Applied, long ClientSeq);

public sealed record SaveResponse(DateTime ServerTime, DateTime ExpiredAt, List<SavedAnswer> Answers);

public sealed record ReviewQuestion(
    Guid Id, int Order, string Type, List<string> SelectedOptions, string? AnswerText, List<string> CorrectOptions,
    List<string> AcceptedAnswers, decimal? CorrectAnswerNumber, bool IsCorrect, decimal Score, string? Explanation);

public sealed record ResultView(
    Guid AttemptId, string Status, string? SubmitReason, bool ScoreVisible, decimal? TotalScore, decimal? MaxScore,
    decimal? Percentage, int? CorrectCount, int TotalQuestion, bool? Passed, bool ReviewAvailable, DateTime? ReviewAvailableAt,
    List<ReviewQuestion>? Questions);

public sealed record StudentExamItem(
    Guid ExamId, string Code, string Name, int MaxAttempts, int UsedAttempts, int RemainingAttempts,
    Guid? InProgressAttemptId, decimal? OfficialScore, string Availability);

/// <summary>Dựng đề 5 câu đủ 4 loại và học viên riêng cho từng test (không lẫn số lượt giữa các test).</summary>
internal sealed class AttemptTestKit(ApiFactory factory, HttpClient admin)
{
    /// <summary>Đáp án đúng của 5 câu theo thứ tự: single B, multiple A+C, true/false TRUE, text "Hà Nội", number 3.5 (±0).</summary>
    public static readonly object[] CorrectAnswers =
    [
        new { selectedOptions = new[] { "B" } },
        new { selectedOptions = new[] { "A", "C" } },
        new { selectedOptions = new[] { "TRUE" } },
        new { answerText = "ha noi" },
        new { answerText = "3,5" },
    ];

    public static async Task<AttemptTestKit> CreateAsync(ApiFactory factory)
    {
        var admin = factory.CreateHttpsClient();
        await admin.LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        return new AttemptTestKit(factory, admin);
    }

    public HttpClient Admin => admin;

    public async Task<(HttpClient Client, Guid UserId, string UserName)> CreateStudentAsync()
    {
        var userName = ApiClientExtensions.UniqueName("hv");
        const string password = "HocVien@2026";
        var (_, created) = await admin.PostJsonAsync<UserWithPassword>(
            "/api/users", new { userName, email = $"{userName}@test.vn", fullName = "Học Viên Thi", password });
        var client = factory.CreateHttpsClient();
        await client.LoginAsync(userName, password);
        return (client, created.Data!.User.Id, userName);
    }

    public async Task<ExamDetail> CreatePublishedExamAsync(
        int maxAttempts = 1,
        string reviewPolicy = "AFTER_SUBMIT",
        string scoreVisibility = "IMMEDIATE",
        int durationMinutes = 30,
        string accessMode = "PUBLIC",
        Guid[]? assignUserIds = null,
        DateTime? endAt = null,
        decimal passPercentage = 60)
    {
        var questionIds = new List<Guid>
        {
            await CreateQuestionAsync(new
            {
                content = "Chọn B",
                questionType = "SINGLE_CHOICE",
                options = new[] { new { content = "A", isCorrect = false }, new { content = "B", isCorrect = true }, new { content = "C", isCorrect = false } },
                explanation = "B là đáp án đúng",
            }),
            await CreateQuestionAsync(new
            {
                content = "Chọn A và C",
                questionType = "MULTIPLE_CHOICE",
                defaultScore = 2,
                options = new[] { new { content = "A", isCorrect = true }, new { content = "B", isCorrect = false }, new { content = "C", isCorrect = true } },
            }),
            await CreateQuestionAsync(new
            {
                content = "Đúng hay sai?",
                questionType = "TRUE_FALSE",
                options = new[] { new { optionCode = "TRUE", content = "", isCorrect = true }, new { optionCode = "FALSE", content = "", isCorrect = false } },
            }),
            await CreateQuestionAsync(new
            {
                content = "Thủ đô?",
                questionType = "FILL_IN",
                answerDataType = "TEXT",
                acceptedAnswers = new[] { "Hà Nội" },
                ignoreAccent = true,
            }),
            await CreateQuestionAsync(new
            {
                content = "7 / 2 = ?",
                questionType = "FILL_IN",
                answerDataType = "NUMBER",
                correctAnswerNumber = 3.5,
            }),
        };

        var (createResponse, exam) = await admin.PostJsonAsync<ExamDetail>("/api/exams", new
        {
            code = ApiClientExtensions.UniqueName("AT-"),
            name = "Đề làm bài",
            maxAttempts,
            accessMode,
            durationMinutes,
            passPercentage,
            reviewPolicy,
            scoreVisibility,
            endAt,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var versionUrl = $"/api/exams/{exam.Data!.Id}/versions/{exam.Data.DraftVersionId}";
        await admin.PostJsonAsync<object>($"{versionUrl}/questions", new { questionIds });
        if (assignUserIds is not null)
        {
            await admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/exams/{exam.Data.Id}/assignments", new { userIds = assignUserIds });
        }

        var (publish, publishBody) = await admin.PostJsonAsync<object>($"{versionUrl}/publish");
        publish.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", publishBody.Errors.Select(e => e.Code)));
        var (_, detail) = await admin.GetJsonAsync<ExamDetail>($"/api/exams/{exam.Data.Id}");
        return detail.Data!;
    }

    public static async Task<AttemptView> StartAsync(HttpClient student, Guid examId, HttpStatusCode expected = HttpStatusCode.Created)
    {
        var (response, body) = await student.PostJsonAsync<AttemptView>($"/api/student/exams/{examId}/start");
        response.StatusCode.Should().Be(expected, string.Join(",", body.Errors.Select(e => e.Code)));
        return body.Data!;
    }

    public static Task<(HttpResponseMessage Response, Envelope<SaveResponse> Body)> SaveAsync(
        HttpClient student, AttemptView attempt, IEnumerable<object> answers) =>
        student.SendJsonAsync<SaveResponse>(HttpMethod.Put, $"/api/student/attempts/{attempt.AttemptId}/answers", new { answers });

    /// <summary>Lưu đáp án đúng cho mọi câu.</summary>
    public static async Task SaveAllCorrectAsync(HttpClient student, AttemptView attempt, long seq = 1)
    {
        var items = attempt.Questions.OrderBy(q => q.Order).Select((q, i) => Merge(CorrectAnswers[i], q.Id, seq)).ToList();
        var (response, body) = await SaveAsync(student, attempt, items);
        response.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", body.Errors.Select(e => e.Code)));
    }

    public static object Merge(object answer, Guid questionId, long clientSeq, bool marked = false)
    {
        var json = JsonSerializer.SerializeToElement(answer, ApiClientExtensions.Json);
        return new
        {
            questionId,
            clientSeq,
            isMarkedForReview = marked,
            selectedOptions = json.TryGetProperty("selectedOptions", out var s) ? s.Deserialize<string[]>() : null,
            answerText = json.TryGetProperty("answerText", out var t) ? t.GetString() : null,
        };
    }

    /// <summary>Duyệt đệ quy mọi khóa trong JSON (docs/08-kiem-thu.md mục 4).</summary>
    public static IReadOnlyCollection<string> AllPropertyNames(string json)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        names.Add(property.Name);
                        Walk(property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item);
                    }

                    break;
            }
        }

        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement);
        return names;
    }

    private async Task<Guid> CreateQuestionAsync(object body)
    {
        var (response, created) = await admin.PostJsonAsync<Questions.QuestionDetail>("/api/questions", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", created.Errors.Select(e => e.Code)));
        return created.Data!.Id;
    }
}

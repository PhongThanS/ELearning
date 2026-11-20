using System.Net;
using ELearning.ApiTests.Attempts;

namespace ELearning.ApiTests.Questions;

public sealed record MyReport(Guid Id, int QuestionOrder, string Reason, string? Note, string Status, DateTime CreatedAt, string? ResolutionNote);

public sealed record ReportedQuestion(
    Guid ExamQuestionId, Guid ExamId, Guid ExamVersionId, string ExamCode, string ExamName, int VersionNumber, int QuestionOrder,
    string ContentPreview, string QuestionType, Guid? SourceQuestionId, bool IsVoided, int ReportCount, int OpenCount,
    DateTime LastReportedAt, List<string> Reasons);

public sealed record ReportedPage(List<ReportedQuestion> Items, int Page, int PageSize, int TotalCount);

public sealed record ReportItem(
    Guid Id, Guid AttemptId, Guid UserId, string UserName, string FullName, int QuestionOrder, string Reason, string? Note,
    string Status, DateTime CreatedAt, DateTime? ResolvedAt, string? ResolvedByName, string? ResolutionNote);

public sealed record ReportedOption(string Code, string Content, bool IsCorrect);

public sealed record ReportedDetail(ReportedQuestion Summary, string Content, List<ReportedOption> Options, List<ReportItem> Reports);

public sealed record ResolveResult(int Changed);

/// <summary>Học sinh báo câu hỏi lỗi sau khi nộp bài; người có quyền xem danh sách và xử lý (D-46).</summary>
[Collection(ApiCollection.Name)]
public class QuestionReportTests(ApiFactory factory)
{
    [Fact]
    public async Task Student_reports_only_after_submit_and_staff_sees_and_resolves_the_question()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, studentId, _) = await kit.CreateStudentAsync();
        var (other, _, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        var url = $"/api/student/attempts/{attempt.AttemptId}/question-reports";

        // Đang thi: không được báo lỗi
        var (during, duringBody) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 1, reason = "WRONG_ANSWER" });
        during.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        duringBody.Errors.Should().ContainSingle(e => e.Code == "QUESTION_REPORT_NOT_ALLOWED");

        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");

        // Sau khi nộp: báo được; báo lại câu đó khi còn chờ thì cập nhật lý do (không tạo thêm)
        var (created, createdBody) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 2, reason = "TYPO", note = "Sai chính tả" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        createdBody.Data!.Status.Should().Be("OPEN");
        var (_, updated) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 2, reason = "WRONG_ANSWER", note = "Đáp án B mới đúng" });
        updated.Data!.Id.Should().Be(createdBody.Data.Id);
        updated.Data.Reason.Should().Be("WRONG_ANSWER");

        var (badOrder, badOrderBody) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 99, reason = "TYPO" });
        badOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badOrderBody.Errors.Should().ContainSingle(e => e.Code == "QUESTION_ORDER_INVALID");
        var (noNote, noNoteBody) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 1, reason = "OTHER" });
        noNote.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noNoteBody.Errors.Should().Contain(e => e.Code == "NOTE_REQUIRED");

        // Lượt thi của người khác → 404; học sinh không vào được danh sách của người xử lý
        (await other.PostJsonAsync<MyReport>(url, new { questionOrder = 1, reason = "TYPO" })).Response.StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await other.GetJsonAsync<List<MyReport>>(url)).Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await student.GetJsonAsync<ReportedPage>("/api/question-reports")).Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Người xử lý: câu xuất hiện trong danh sách chờ, chi tiết có đáp án + người báo
        var (_, page) = await kit.Admin.GetJsonAsync<ReportedPage>($"/api/question-reports?status=OPEN&examId={exam.Id}");
        var row = page.Data!.Items.Should().ContainSingle().Subject;
        row.ReportCount.Should().Be(1);
        row.OpenCount.Should().Be(1);
        row.Reasons.Should().Equal("WRONG_ANSWER");
        row.ExamCode.Should().Be(exam.Code);
        var (_, openCount) = await kit.Admin.GetJsonAsync<int>("/api/question-reports/open-count");
        openCount.Data.Should().BeGreaterThanOrEqualTo(1);

        var (_, detail) = await kit.Admin.GetJsonAsync<ReportedDetail>($"/api/question-reports/{row.ExamQuestionId}");
        detail.Data!.Reports.Should().ContainSingle(r => r.UserId == studentId && r.Note == "Đáp án B mới đúng" && r.QuestionOrder == 2);
        detail.Data.Options.Should().Contain(o => o.IsCorrect);

        // Đánh dấu không lỗi → học sinh thấy kết quả, không sửa được nữa; mở lại thì chờ xử lý lại
        var (_, resolved) = await kit.Admin.PostJsonAsync<ResolveResult>(
            $"/api/question-reports/{row.ExamQuestionId}/resolve", new { status = "REJECTED", note = "Đáp án đúng là A" });
        resolved.Data!.Changed.Should().Be(1);
        var (_, mine) = await student.GetJsonAsync<List<MyReport>>(url);
        mine.Data.Should().ContainSingle(r => r.Status == "REJECTED" && r.ResolutionNote == "Đáp án đúng là A");
        var (closed, closedBody) = await student.PostJsonAsync<MyReport>(url, new { questionOrder = 2, reason = "TYPO" });
        closed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        closedBody.Errors.Should().ContainSingle(e => e.Code == "QUESTION_REPORT_CLOSED");

        var (_, emptyOpen) = await kit.Admin.GetJsonAsync<ReportedPage>($"/api/question-reports?status=OPEN&examId={exam.Id}");
        emptyOpen.Data!.Items.Should().BeEmpty();
        var (_, reopened) = await kit.Admin.PostJsonAsync<ResolveResult>(
            $"/api/question-reports/{row.ExamQuestionId}/resolve", new { status = "OPEN" });
        reopened.Data!.Changed.Should().Be(1);
        var (_, again) = await kit.Admin.GetJsonAsync<ReportedPage>($"/api/question-reports?status=OPEN&examId={exam.Id}");
        again.Data!.Items.Should().ContainSingle();
    }
}

using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;

namespace ELearning.ApiTests.Videos;

public sealed record VideoPracticeView(Guid ExamId, string ExamCode, string ExamName, bool AlreadyAttempted, int AttemptCount, bool Ready);

public sealed record VideoWithPracticeView(Guid Id, Guid? PracticeExamId, string? PracticeExamName, string RowVersion);

public sealed record VideoWithHomeworkView(Guid Id, string? HomeworkUrl, string RowVersion);

public sealed record PracticeAttemptRow(Guid AttemptId, string Status);

public sealed record PracticeExamDetailView(Guid ExamId, int UsedAttempts, List<PracticeAttemptRow> Attempts);

/// <summary>Đề luyện gắn với video (D-38): học viên bấm "Luyện đề" được tự thêm vào đề, làm lại thì lượt cũ bị hủy.</summary>
[Collection(ApiCollection.Name)]
public class VideoPracticeTests(ApiFactory factory)
{
    private const string Video = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    [Fact]
    public async Task Practice_button_assigns_the_exam_and_restart_cancels_previous_attempts()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (other, otherId, _) = await kit.CreateStudentAsync();
        var (student, _, _) = await kit.CreateStudentAsync();
        // Đề luyện chỉ gán cho người khác: học viên chưa thấy đề này
        var exam = await kit.CreatePublishedExamAsync(kind: "PRACTICE", accessMode: "ASSIGNED", assignUserIds: [otherId]);
        var (created, createdBody) = await kit.Admin.PostJsonAsync<VideoWithPracticeView>("/api/videos", new
        {
            title = ApiClientExtensions.UniqueName("Bài "),
            videoUrl = Video,
            practiceExamId = exam.Id,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", createdBody.Errors.Select(e => e.Code)));
        createdBody.Data!.PracticeExamId.Should().Be(exam.Id);
        var practiceUrl = $"/api/student/videos/{createdBody.Data.Id}/practice";

        var (_, before) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");
        var (first, firstBody) = await student.PostJsonAsync<VideoPracticeView>(practiceUrl, new { restart = false });
        var (_, after) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");

        before.Data!.Items.Should().NotContain(e => e.ExamId == exam.Id);
        first.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", firstBody.Errors.Select(e => e.Code)));
        firstBody.Data!.Should().Match<VideoPracticeView>(p => p.Ready && !p.AlreadyAttempted && p.ExamId == exam.Id);
        after.Data!.Items.Should().ContainSingle(e => e.ExamId == exam.Id, "đề được thêm vào danh sách bài tập của học viên");

        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");

        var (_, ask) = await student.PostJsonAsync<VideoPracticeView>(practiceUrl, new { restart = false });
        var (_, restart) = await student.PostJsonAsync<VideoPracticeView>(practiceUrl, new { restart = true });
        var (_, detail) = await student.GetJsonAsync<PracticeExamDetailView>($"/api/student/exams/{exam.Id}");
        var (_, otherList) = await other.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");

        ask.Data!.Should().Match<VideoPracticeView>(p => p.AlreadyAttempted && !p.Ready && p.AttemptCount == 1, "đã làm thì hỏi làm lại");
        restart.Data!.Should().Match<VideoPracticeView>(p => p.Ready && p.AlreadyAttempted);
        detail.Data!.Attempts.Should().BeEmpty("lượt cũ đã hủy khi làm lại và không hiện cho học viên");
        detail.Data.UsedAttempts.Should().Be(0);
        otherList.Data!.Items.Should().Contain(e => e.ExamId == exam.Id, "người được gán từ trước vẫn giữ quyền");

        var again = await AttemptTestKit.StartAsync(student, exam.Id);
        again.AttemptNumber.Should().Be(2, "lượt mới vẫn đánh số tiếp, lượt cũ chỉ bị hủy chứ không xóa");
    }

    [Fact]
    public async Task Only_practice_exams_can_be_linked_and_videos_of_other_classes_are_hidden()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        var graded = await kit.CreatePublishedExamAsync();
        var practice = await kit.CreatePublishedExamAsync(kind: "PRACTICE");
        var (_, classroom) = await kit.Admin.PostJsonAsync<ClassItemRef>("/api/classes", new { code = ApiClientExtensions.UniqueName("VP-"), name = "Lớp khác" });

        var (wrong, wrongBody) = await kit.Admin.PostJsonAsync<object>("/api/videos", new { title = "Bài A", videoUrl = Video, practiceExamId = graded.Id });
        var (_, hidden) = await kit.Admin.PostJsonAsync<VideoWithPracticeView>("/api/videos", new
        {
            title = "Bài lớp khác",
            videoUrl = Video,
            practiceExamId = practice.Id,
            classroomIds = new[] { classroom.Data!.Id },
        });
        var (_, noExam) = await kit.Admin.PostJsonAsync<VideoWithPracticeView>("/api/videos", new { title = "Bài không đề", videoUrl = Video });
        var (hiddenPractice, _) = await student.PostJsonAsync<object>($"/api/student/videos/{hidden.Data!.Id}/practice", new { });
        var (noExamPractice, noExamBody) = await student.PostJsonAsync<object>($"/api/student/videos/{noExam.Data!.Id}/practice", new { });

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrongBody.Errors.Should().Contain(e => e.Code == "PRACTICE_EXAM_INVALID");
        hiddenPractice.StatusCode.Should().Be(HttpStatusCode.NotFound, "video của lớp khác");
        noExamPractice.StatusCode.Should().Be(HttpStatusCode.NotFound);
        noExamBody.Errors.Should().Contain(e => e.Code == "PRACTICE_EXAM_NOT_SET");
    }

    [Fact]
    public async Task Homework_pdf_link_is_saved_on_create_and_update_and_validated()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();
        const string pdf = "https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUv/view?usp=sharing";
        var title = ApiClientExtensions.UniqueName("BTVN ");
        var (bad, badBody) = await kit.Admin.PostJsonAsync<object>("/api/videos", new { title, videoUrl = Video, homeworkUrl = "https://example.com/a.pdf" });
        var (created, createdBody) = await kit.Admin.PostJsonAsync<VideoWithHomeworkView>("/api/videos", new { title, videoUrl = Video, homeworkUrl = pdf });

        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badBody.Errors.Should().Contain(e => e.Code == "INVALID_HOMEWORK_URL");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        createdBody.Data!.HomeworkUrl.Should().Be(pdf);
        var (_, seen) = await student.GetJsonAsync<VideoWithHomeworkView>($"/api/student/videos/{createdBody.Data.Id}");
        seen.Data!.HomeworkUrl.Should().Be(pdf, "học viên thấy nút Bài tập về nhà");

        var (updated, updatedBody) = await kit.Admin.SendJsonAsync<VideoWithHomeworkView>(HttpMethod.Put, $"/api/videos/{createdBody.Data.Id}", new
        {
            title,
            videoUrl = Video,
            displayOrder = 0,
            isActive = true,
            rowVersion = createdBody.Data.RowVersion,
            homeworkUrl = (string?)null,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(",", updatedBody.Errors.Select(e => e.Code)));
        updatedBody.Data!.HomeworkUrl.Should().BeNull("sửa video bỏ trống ô thì gỡ bài tập về nhà");
    }
}

public sealed record ClassItemRef(Guid Id, string Code);

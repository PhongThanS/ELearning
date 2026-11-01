using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Classes;

namespace ELearning.ApiTests.Videos;

public sealed record VideoClassroomView(Guid Id, string Code, string Name);

public sealed record VideoWithClassesView(Guid Id, string Title, List<VideoClassroomView> Classrooms, string RowVersion);

/// <summary>Bài giảng gắn nhiều lớp: học viên của bất kỳ lớp nào trong đó xem được; không gắn lớp = bài giảng chung.</summary>
[Collection(ApiCollection.Name)]
public class VideoClassroomTests(ApiFactory factory)
{
    private const string Video = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    [Fact]
    public async Task Lesson_for_many_classes_is_visible_to_students_of_any_of_them_only()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var classA = await CreateClassAsync(kit.Admin, "Lớp A");
        var classB = await CreateClassAsync(kit.Admin, "Lớp B");
        var (studentA, idA, _) = await kit.CreateStudentAsync();
        var (studentB, idB, _) = await kit.CreateStudentAsync();
        var (outsider, _, _) = await kit.CreateStudentAsync();
        await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{classA.Id}/students", new { userIds = new[] { idA } });
        await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{classB.Id}/students", new { userIds = new[] { idB } });

        var lesson = await CreateLessonAsync(kit.Admin, [classA.Id, classB.Id, classA.Id]);
        var common = await CreateLessonAsync(kit.Admin, []);

        lesson.Classrooms.Select(c => c.Id).Should().BeEquivalentTo([classA.Id, classB.Id], "bỏ lớp trùng");
        lesson.Classrooms.Should().Contain(c => c.Code == classA.Code && c.Name == "Lớp A");
        (await VisibleAsync(studentA)).Should().Contain([lesson.Id, common.Id]);
        (await VisibleAsync(studentB)).Should().Contain([lesson.Id, common.Id]);
        (await VisibleAsync(outsider)).Should().Contain(common.Id).And.NotContain(lesson.Id);

        var (_, byClassB) = await kit.Admin.GetJsonAsync<Paged<VideoWithClassesView>>($"/api/videos?classroomId={classB.Id}&pageSize=100");
        byClassB.Data!.Items.Select(v => v.Id).Should().Contain(lesson.Id).And.NotContain(common.Id);

        // Bỏ lớp B: học viên lớp B không còn thấy
        var (updated, updatedBody) = await kit.Admin.SendJsonAsync<VideoWithClassesView>(HttpMethod.Put, $"/api/videos/{lesson.Id}", new
        {
            title = lesson.Title,
            videoUrl = Video,
            classroomIds = new[] { classA.Id },
            displayOrder = 0,
            isActive = true,
            rowVersion = lesson.RowVersion,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedBody.Data!.Classrooms.Should().ContainSingle(c => c.Id == classA.Id);
        (await VisibleAsync(studentA)).Should().Contain(lesson.Id);
        (await VisibleAsync(studentB)).Should().NotContain(lesson.Id);
    }

    [Fact]
    public async Task Unknown_class_is_rejected()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);

        var (response, body) = await kit.Admin.PostJsonAsync<VideoWithClassesView>("/api/videos", new
        {
            title = "Bài lớp lạ",
            videoUrl = Video,
            classroomIds = new[] { Guid.NewGuid() },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Errors.Should().Contain(e => e.Code == "CLASSROOM_NOT_FOUND" && e.Field == "classroomIds");
    }

    [Fact]
    public async Task Class_used_by_a_lesson_cannot_be_deleted_and_lesson_with_classes_can_be_deleted()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var classroom = await CreateClassAsync(kit.Admin, "Lớp có video");
        var lesson = await CreateLessonAsync(kit.Admin, [classroom.Id]);

        var (blocked, blockedBody) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/classes/{classroom.Id}");
        var (deletedLesson, _) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/videos/{lesson.Id}");
        var (deletedClass, _) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/classes/{classroom.Id}");

        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict, "bỏ lớp ngầm có thể biến video thành bài giảng chung");
        blockedBody.Errors.Single().Code.Should().Be("CLASSROOM_IN_USE");
        deletedLesson.StatusCode.Should().Be(HttpStatusCode.NoContent);
        deletedClass.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<List<Guid>> VisibleAsync(HttpClient student)
    {
        var (response, body) = await student.GetJsonAsync<Paged<VideoWithClassesView>>("/api/student/videos?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return body.Data!.Items.Select(v => v.Id).ToList();
    }

    private static async Task<VideoWithClassesView> CreateLessonAsync(HttpClient admin, Guid[] classroomIds)
    {
        var (response, body) = await admin.PostJsonAsync<VideoWithClassesView>("/api/videos", new
        {
            title = ApiClientExtensions.UniqueName("Bài "),
            videoUrl = Video,
            classroomIds,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        return body.Data!;
    }

    private static async Task<ClassItem> CreateClassAsync(HttpClient admin, string name)
    {
        var (response, body) = await admin.PostJsonAsync<ClassItem>("/api/classes", new
        {
            code = ApiClientExtensions.UniqueName("CL-"),
            name,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        return body.Data!;
    }
}

using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;

namespace ELearning.ApiTests.Classes;

public sealed record ClassItem(
    Guid Id, string Code, string Name, string? SchoolYear, DateOnly? StartDate, DateOnly? EndDate, bool IsActive,
    int StudentCount, int ExamCount, string RowVersion);

public sealed record MyClassItem(Guid Id, string Code, string Name, int StudentCount, int ExamCount);

/// <summary>Lớp học: học viên ↔ lớp nhiều-nhiều, gán đề cho lớp (D-28).</summary>
[Collection(ApiCollection.Name)]
public class ClassroomTests(ApiFactory factory)
{
    [Fact]
    public async Task Student_belongs_to_many_classes_and_class_has_many_students()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, studentA, _) = await kit.CreateStudentAsync();
        var (_, studentB, _) = await kit.CreateStudentAsync();
        var math = await CreateClassAsync(kit.Admin, "Toán 10A1");
        var physics = await CreateClassAsync(kit.Admin, "Lý 10A1");

        var (addMath, mathBody) = await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{math.Id}/students", new { userIds = new[] { studentA, studentB, studentA } });
        var (addPhysics, physicsBody) = await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{physics.Id}/students", new { userIds = new[] { studentA } });
        var (_, mathStudents) = await kit.Admin.GetJsonAsync<Paged<object>>($"/api/classes/{math.Id}/students");

        addMath.StatusCode.Should().Be(HttpStatusCode.OK);
        mathBody.Data!.StudentCount.Should().Be(2, "trùng học viên trong cùng yêu cầu chỉ thêm một lần");
        addPhysics.StatusCode.Should().Be(HttpStatusCode.OK);
        physicsBody.Data!.StudentCount.Should().Be(1);
        mathStudents.Data!.TotalCount.Should().Be(2);

        // Rút khỏi một lớp không ảnh hưởng lớp kia
        var (removed, removedBody) = await kit.Admin.SendJsonAsync<ClassItem>(HttpMethod.Delete, $"/api/classes/{math.Id}/students/{studentA}");
        var (_, physicsAfter) = await kit.Admin.GetJsonAsync<ClassItem>($"/api/classes/{physics.Id}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        removedBody.Data!.StudentCount.Should().Be(1);
        physicsAfter.Data!.StudentCount.Should().Be(1);
    }

    [Fact]
    public async Task Class_validation_rejects_duplicate_code_bad_dates_and_non_students()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var created = await CreateClassAsync(kit.Admin, "Lớp kiểm tra");
        var (_, admins) = await kit.Admin.GetJsonAsync<Paged<UserListRow>>("/api/users?roleCode=ADMIN");
        var adminId = admins.Data!.Items[0].Id;

        var (duplicate, duplicateBody) = await kit.Admin.PostJsonAsync<object>("/api/classes", new { code = created.Code, name = "Trùng" });
        var (badDates, badDatesBody) = await kit.Admin.PostJsonAsync<object>("/api/classes", new
        {
            code = ApiClientExtensions.UniqueName("CL-"),
            name = "Sai ngày",
            startDate = "2026-09-10",
            endDate = "2026-09-01",
        });
        var (nonStudent, nonStudentBody) = await kit.Admin.PostJsonAsync<object>($"/api/classes/{created.Id}/students", new { userIds = new[] { adminId } });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicateBody.Errors.Single().Code.Should().Be("DUPLICATE_CODE");
        badDates.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badDatesBody.Errors.Should().Contain(e => e.Code == "DATE_RANGE_INVALID" && e.Field == "endDate");
        nonStudent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        nonStudentBody.Errors.Single().Field.Should().Be("userIds");
    }

    [Fact]
    public async Task Exam_assigned_to_class_is_visible_only_to_current_members_of_active_class()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (memberClient, memberId, _) = await kit.CreateStudentAsync();
        var (outsiderClient, _, _) = await kit.CreateStudentAsync();
        var classroom = await CreateClassAsync(kit.Admin, "Lớp có đề");
        await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{classroom.Id}/students", new { userIds = new[] { memberId } });
        var exam = await kit.CreatePublishedExamAsync(accessMode: "ASSIGNED", assignClassroomIds: [classroom.Id]);

        (await SeesExamAsync(memberClient, exam.Id)).Should().BeTrue();
        (await SeesExamAsync(outsiderClient, exam.Id)).Should().BeFalse();
        var (outsiderStart, _) = await outsiderClient.PostJsonAsync<object>($"/api/student/exams/{exam.Id}/start");
        outsiderStart.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // "Lớp của tôi" và lọc đề theo lớp
        var (_, mine) = await memberClient.GetJsonAsync<List<MyClassItem>>("/api/student/classes");
        mine.Data!.Should().ContainSingle(c => c.Id == classroom.Id && c.ExamCount == 1);
        var (_, byClass) = await memberClient.GetJsonAsync<Paged<StudentExamItem>>($"/api/student/exams?classroomId={classroom.Id}");
        byClass.Data!.Items.Should().ContainSingle(e => e.ExamId == exam.Id);
        var (_, outsiderByClass) = await outsiderClient.GetJsonAsync<Paged<StudentExamItem>>($"/api/student/exams?classroomId={classroom.Id}");
        outsiderByClass.Data!.TotalCount.Should().Be(0, "không thuộc lớp thì không lọc được đề của lớp");

        // Tắt lớp: mất quyền thấy đề; bật lại: thấy lại
        await kit.Admin.SendJsonAsync<ClassItem>(HttpMethod.Patch, $"/api/classes/{classroom.Id}/status", new { isActive = false });
        (await SeesExamAsync(memberClient, exam.Id)).Should().BeFalse();
        var (_, mineInactive) = await memberClient.GetJsonAsync<List<MyClassItem>>("/api/student/classes");
        mineInactive.Data!.Should().NotContain(c => c.Id == classroom.Id);
        await kit.Admin.SendJsonAsync<ClassItem>(HttpMethod.Patch, $"/api/classes/{classroom.Id}/status", new { isActive = true });
        (await SeesExamAsync(memberClient, exam.Id)).Should().BeTrue();

        // Rút khỏi lớp: mất quyền thấy đề
        await kit.Admin.SendJsonAsync<ClassItem>(HttpMethod.Delete, $"/api/classes/{classroom.Id}/students/{memberId}");
        (await SeesExamAsync(memberClient, exam.Id)).Should().BeFalse();

        var (_, assignments) = await kit.Admin.GetJsonAsync<AssignmentsView>($"/api/exams/{exam.Id}/assignments");
        assignments.Data!.Classrooms.Should().ContainSingle(c => c.Id == classroom.Id);
    }

    [Fact]
    public async Task Student_cannot_manage_classes()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (student, _, _) = await kit.CreateStudentAsync();

        var (list, _) = await student.GetJsonAsync<object>("/api/classes");
        var (create, _) = await student.PostJsonAsync<object>("/api/classes", new { code = "HACK", name = "X" });

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<ClassItem> CreateClassAsync(HttpClient admin, string name)
    {
        var (response, body) = await admin.PostJsonAsync<ClassItem>("/api/classes", new
        {
            code = ApiClientExtensions.UniqueName("CL-"),
            name,
            schoolYear = "2026-2027",
            startDate = "2026-09-05",
            endDate = "2027-05-31",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        return body.Data!;
    }

    private static async Task<bool> SeesExamAsync(HttpClient student, Guid examId)
    {
        var (_, body) = await student.GetJsonAsync<Paged<StudentExamItem>>("/api/student/exams?pageSize=100");
        return body.Data!.Items.Exists(e => e.ExamId == examId);
    }
}

public sealed record UserListRow(Guid Id, string UserName);

public sealed record AssignedClassroomView(Guid Id, string Code, string Name, int StudentCount);

public sealed record AssignmentsView(string AccessMode, List<AssignedClassroomView> Classrooms);

using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;
using ELearning.Domain.Identity;

namespace ELearning.ApiTests.Classes;

public sealed record AttendanceClassItem(Guid Id, string Code, string Name, int StudentCount, string? HomeroomTeacherName);

public sealed record AttendanceStudentItem(
    Guid StudentId, string UserName, string FullName, string? ParentName, string? ParentPhone,
    string? Status, string? HomeworkStatus, decimal? HomeworkScore, string? Note, bool InClass);

public sealed record AttendanceSessionView(
    Guid ClassroomId, DateOnly SessionDate, bool Saved, string? Topic, string? NextHomework, List<AttendanceStudentItem> Students);

public sealed record AttendanceHistoryItem(DateOnly SessionDate, string? Status, decimal? HomeworkScore);

public sealed record AttendanceHistoryView(Guid StudentId, int Year, int Month, decimal? AverageScore, int? ClassRank, int RankedCount, List<AttendanceHistoryItem> Items);

/// <summary>Điểm danh + BTVN theo buổi (D-31).</summary>
[Collection(ApiCollection.Name)]
public class AttendanceTests(ApiFactory factory)
{
    private DateTime Now => factory.Time.GetUtcNow().UtcDateTime;

    private string Today => Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Teacher_role_gets_attendance_permission_by_default()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, roles) = await kit.Admin.GetJsonAsync<List<RoleItem>>("/api/roles");

        roles.Data!.Single(r => r.Code == Role.TeacherCode).Permissions.Should().Contain(Permissions.AttendanceTake);
    }

    [Fact]
    public async Task Teacher_takes_attendance_only_for_own_classes()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, studentA, _) = await kit.CreateStudentAsync();
        var (_, studentB, _) = await kit.CreateStudentAsync();
        var mine = await CreateClassAsync(kit, teacherId, studentA, studentB);
        var other = await CreateClassAsync(kit, homeroomTeacherId: null, studentA);

        var (_, classes) = await teacher.GetJsonAsync<List<AttendanceClassItem>>("/api/attendance/classes");
        classes.Data!.Should().ContainSingle().Which.Id.Should().Be(mine);

        var (otherSession, otherBody) = await teacher.GetJsonAsync<object>($"/api/attendance/classes/{other}/sessions/{Today}");
        otherSession.StatusCode.Should().Be(HttpStatusCode.NotFound, "lớp không do mình chủ nhiệm");
        otherBody.Errors.Single().Code.Should().Be("CLASSROOM_NOT_FOUND");
        var (otherSave, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{other}/sessions/{Today}", new { records = Array.Empty<object>() });
        otherSave.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Admin (Attendance.ManageAll) thấy mọi lớp
        var (adminSession, _) = await kit.Admin.GetJsonAsync<AttendanceSessionView>($"/api/attendance/classes/{other}/sessions/{Today}");
        adminSession.StatusCode.Should().Be(HttpStatusCode.OK);

        var (student, _, _) = await kit.CreateStudentAsync();
        var (forbidden, _) = await student.GetJsonAsync<object>("/api/attendance/classes");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Save_upserts_records_and_reset_clears_them()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, studentA, _) = await kit.CreateStudentAsync();
        var (_, studentB, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, studentA, studentB);
        var url = $"/api/attendance/classes/{classId}/sessions/{Today}";

        var (_, empty) = await teacher.GetJsonAsync<AttendanceSessionView>(url);
        empty.Data!.Saved.Should().BeFalse();
        empty.Data.Students.Should().HaveCount(2).And.OnlyContain(s => s.Status == null && s.InClass);

        var (save, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, url, new
        {
            topic = "Hàm số bậc hai",
            nextHomework = "Bài 1–5 trang 40",
            records = new object[]
            {
                new { studentId = studentA, status = "PRESENT", homeworkStatus = "EXCELLENT", homeworkScore = 9.5m, note = "Hăng hái" },
                new { studentId = studentB, status = "LATE", homeworkStatus = "PARTIAL", homeworkScore = 6m },
            },
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        // Lưu lại một học sinh (tự động lưu): chỉ dòng đó đổi
        await teacher.SendJsonAsync<object>(HttpMethod.Put, url, new
        {
            topic = "Hàm số bậc hai",
            nextHomework = "Bài 1–5 trang 40",
            records = new[] { new { studentId = studentB, status = "PRESENT", homeworkStatus = "DONE", homeworkScore = 7.5m } },
        });

        var (_, saved) = await teacher.GetJsonAsync<AttendanceSessionView>(url);
        saved.Data!.Saved.Should().BeTrue();
        saved.Data.Topic.Should().Be("Hàm số bậc hai");
        saved.Data.Students.Single(s => s.StudentId == studentA).Should()
            .Match<AttendanceStudentItem>(s => s.Status == "PRESENT" && s.HomeworkStatus == "EXCELLENT" && s.HomeworkScore == 9.5m && s.Note == "Hăng hái");
        saved.Data.Students.Single(s => s.StudentId == studentB).Should()
            .Match<AttendanceStudentItem>(s => s.Status == "PRESENT" && s.HomeworkStatus == "DONE" && s.HomeworkScore == 7.5m);

        var (reset, resetBody) = await teacher.PostJsonAsync<AttendanceSessionView>($"{url}/reset");
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        resetBody.Data!.Topic.Should().BeNull();
        resetBody.Data.Students.Should().OnlyContain(s => s.Status == null && s.HomeworkScore == null && s.Note == null);
    }

    [Fact]
    public async Task Save_rejects_bad_score_future_date_and_outsiders()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, member, _) = await kit.CreateStudentAsync();
        var (_, outsider, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, member);
        var url = $"/api/attendance/classes/{classId}/sessions/{Today}";

        var (badScore, badScoreBody) = await teacher.SendJsonAsync<object>(HttpMethod.Put, url, new
        {
            records = new[] { new { studentId = member, homeworkScore = 7.3m } },
        });
        var (tooHigh, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, url, new
        {
            records = new[] { new { studentId = member, homeworkScore = 10.5m } },
        });
        var (notMember, notMemberBody) = await teacher.SendJsonAsync<object>(HttpMethod.Put, url, new
        {
            records = new[] { new { studentId = outsider, status = "PRESENT" } },
        });
        var future = Now.AddDays(5).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var (futureSave, futureBody) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/sessions/{future}", new
        {
            records = new[] { new { studentId = member, status = "PRESENT" } },
        });

        badScore.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badScoreBody.Errors.Should().Contain(e => e.Code == "SCORE_INVALID");
        tooHigh.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notMember.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notMemberBody.Errors.Single().Field.Should().Be("records");
        futureSave.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        futureBody.Errors.Single().Field.Should().Be("date");
    }

    [Fact]
    public async Task History_ranks_monthly_average_and_contact_shows_in_session()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, top, _) = await kit.CreateStudentAsync();
        var (_, second, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, top, second);
        var month = Now.AddDays(-40);
        var day1 = new DateOnly(month.Year, month.Month, 3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var day2 = new DateOnly(month.Year, month.Month, 10).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var (day, topScore, secondScore) in new[] { (day1, 9m, 6m), (day2, 10m, 7m) })
        {
            var (save, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/sessions/{day}", new
            {
                records = new object[]
                {
                    new { studentId = top, status = "PRESENT", homeworkScore = topScore },
                    new { studentId = second, status = "UNEXCUSED", homeworkScore = secondScore },
                },
            });
            save.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var monthText = $"{month.Year}-{month.Month:00}";
        var (_, history) = await teacher.GetJsonAsync<AttendanceHistoryView>($"/api/attendance/classes/{classId}/students/{second}/history?month={monthText}");
        history.Data!.Items.Should().HaveCount(2);
        history.Data.AverageScore.Should().Be(6.5m);
        history.Data.ClassRank.Should().Be(2);
        history.Data.RankedCount.Should().Be(2);
        history.Data.Items.Should().OnlyContain(i => i.Status == "UNEXCUSED");

        var (contact, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/students/{top}/contact",
            new { parentName = "Nguyễn Văn Bố", parentPhone = "0912 345 678" });
        var (badPhone, badPhoneBody) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/students/{top}/contact",
            new { parentPhone = "abc" });
        var (_, session) = await teacher.GetJsonAsync<AttendanceSessionView>($"/api/attendance/classes/{classId}/sessions/{day1}");

        contact.StatusCode.Should().Be(HttpStatusCode.OK);
        badPhone.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badPhoneBody.Errors.Single().Code.Should().Be("PHONE_INVALID");
        session.Data!.Students.Single(s => s.StudentId == top).Should()
            .Match<AttendanceStudentItem>(s => s.ParentName == "Nguyễn Văn Bố" && s.ParentPhone == "0912 345 678");

        var export = await teacher.GetAsync($"/api/attendance/classes/{classId}/sessions/{day1}/export");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task Class_with_attendance_cannot_be_deleted()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, student, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, homeroomTeacherId: null, student);
        await kit.Admin.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/sessions/{Today}", new
        {
            records = new[] { new { studentId = student, status = "PRESENT" } },
        });

        var (delete, body) = await kit.Admin.SendJsonAsync<object>(HttpMethod.Delete, $"/api/classes/{classId}");

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Errors.Single().Code.Should().Be("CLASSROOM_IN_USE");
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateTeacherAsync(AttemptTestKit kit)
    {
        var userName = ApiClientExtensions.UniqueName("gv");
        const string password = "GiaoVien@2026";
        var (_, created) = await kit.Admin.PostJsonAsync<UserWithPassword>(
            "/api/users", new { userName, email = $"{userName}@test.vn", fullName = "Giáo Viên Chủ Nhiệm", password });
        var (_, roles) = await kit.Admin.GetJsonAsync<List<RoleItem>>("/api/roles");
        var teacherRoleId = roles.Data!.Single(r => r.Code == Role.TeacherCode).Id;
        var (setRoles, _) = await kit.Admin.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/users/{created.Data!.User.Id}/roles", new { roleIds = new[] { teacherRoleId } });
        setRoles.StatusCode.Should().Be(HttpStatusCode.OK);

        var client = factory.CreateHttpsClient();
        await client.LoginAsync(userName, password);
        return (client, created.Data.User.Id);
    }

    private static async Task<Guid> CreateClassAsync(AttemptTestKit kit, Guid? homeroomTeacherId, params Guid[] studentIds)
    {
        var (response, body) = await kit.Admin.PostJsonAsync<ClassItem>("/api/classes", new
        {
            code = ApiClientExtensions.UniqueName("DD-"),
            name = "Lớp điểm danh",
            homeroomTeacherId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{body.Data!.Id}/students", new { userIds = studentIds });
        return body.Data.Id;
    }
}

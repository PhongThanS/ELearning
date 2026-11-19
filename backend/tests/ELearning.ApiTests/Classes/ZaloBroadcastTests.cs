using System.Net;
using ELearning.ApiTests.Admin;
using ELearning.ApiTests.Attempts;
using ELearning.Application.Zalo;
using ELearning.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.ApiTests.Classes;

public sealed record ZaloRecipientItem(Guid StudentId, string StudentName, string? SkipReason, Dictionary<string, string>? Data);

public sealed record ZaloPreviewView(int SendableCount, bool AlreadySent, List<ZaloRecipientItem> Recipients);

public sealed record ZaloBroadcastView(Guid Id, string Kind, int Total, int Pending, int Sent, int Failed, int Cancelled);

public sealed record ZaloCreatedView(ZaloBroadcastView Broadcast, List<ZaloRecipientItem> Skipped);

public sealed record ZaloMessageItem(Guid Id, string Status, string Phone, string? ErrorCode);

public sealed record ZaloDetailView(ZaloBroadcastView Broadcast, List<ZaloMessageItem> Messages);

/// <summary>Gửi tin Zalo cho phụ huynh cả lớp (D-45): người nhận, soạn tin, hàng đợi, lỗi và gửi lại.</summary>
[Collection(ApiCollection.Name)]
public class ZaloBroadcastTests(ApiFactory factory)
{
    private string Today => factory.Time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Teacher_role_gets_zalo_permission_and_students_are_forbidden()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (_, roles) = await kit.Admin.GetJsonAsync<List<RoleItem>>("/api/roles");
        roles.Data!.Single(r => r.Code == Role.TeacherCode).Permissions.Should().Contain(Permissions.ZaloSend);

        var (student, _, _) = await kit.CreateStudentAsync();
        var (forbidden, _) = await student.GetJsonAsync<object>("/api/zalo/status");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_only_reaches_own_class()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, student, _) = await kit.CreateStudentAsync();
        var other = await CreateClassAsync(kit, null, student);

        var (preview, body) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{other}/preview", new { kind = "ANNOUNCEMENT", content = "Nghỉ học" });
        preview.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Errors.Single().Code.Should().Be("CLASSROOM_NOT_FOUND");
        var (create, _) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{other}/broadcasts", new { kind = "ANNOUNCEMENT", content = "Nghỉ học" });
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
        teacherId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Daily_report_builds_one_personal_message_per_marked_student_and_sends_it()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, a, _) = await kit.CreateStudentAsync();
        var (_, b, _) = await kit.CreateStudentAsync();
        var (_, c, _) = await kit.CreateStudentAsync();
        var (_, d, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, a, b, c, d);
        var phoneA = RandomPhone();
        await SetContactAsync(teacher, classId, a, "Cô Lan", phoneA);
        await SetContactAsync(teacher, classId, b, "Chú Bình", "12345678");
        await SetContactAsync(teacher, classId, d, "Bà Dung", RandomPhone());
        // c không có SĐT phụ huynh; d có SĐT nhưng chưa điểm danh
        var (save, _) = await teacher.SendJsonAsync<object>(HttpMethod.Put, $"/api/attendance/classes/{classId}/sessions/{Today}", new
        {
            topic = "Hàm số bậc hai",
            nextHomework = "Bài 1–5 trang 40",
            records = new object[]
            {
                new { studentId = a, status = "PRESENT", homeworkStatus = "EXCELLENT", homeworkScore = 9.5m, note = "Hăng hái phát biểu" },
                new { studentId = b, status = "LATE", homeworkStatus = "DONE", homeworkScore = 7m },
                new { studentId = c, status = "PRESENT", homeworkStatus = "DONE", homeworkScore = 8m },
            },
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var request = new { kind = "DAILY_REPORT", sessionDate = Today, deadline = "18h Thứ 7", attention = "Mang máy tính" };
        var (_, preview) = await teacher.PostJsonAsync<ZaloPreviewView>($"/api/zalo/classes/{classId}/preview", request);
        preview.Data!.SendableCount.Should().Be(1);
        preview.Data.AlreadySent.Should().BeFalse();
        Reason(preview.Data.Recipients, a).Should().BeNull();
        Reason(preview.Data.Recipients, b).Should().Be("INVALID_PHONE");
        Reason(preview.Data.Recipients, c).Should().Be("NO_PHONE");
        Reason(preview.Data.Recipients, d).Should().Be("NOT_MARKED");

        var (create, created) = await teacher.PostJsonAsync<ZaloCreatedView>($"/api/zalo/classes/{classId}/broadcasts", request);
        create.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", created.Errors.Select(e => e.Code)));
        created.Data!.Broadcast.Should().Match<ZaloBroadcastView>(x => x.Total == 1 && x.Pending == 1 && x.Sent == 0);
        created.Data.Skipped.Should().HaveCount(3);

        await DispatchAsync();

        var sent = factory.ZaloSender.Sent.Single(s => s.Phone == "84" + phoneA[1..]);
        sent.Data["studentName"].Should().NotBeNullOrWhiteSpace();
        sent.Data.Should().Contain("parentName", "Cô Lan").And.Contain("score", "9,5").And.Contain("attendance", "Có mặt")
            .And.Contain("topic", "Hàm số bậc hai").And.Contain("homework", "Bài 1–5 trang 40")
            .And.Contain("deadline", "18h Thứ 7").And.Contain("note", "Hăng hái phát biểu");

        var (_, detail) = await teacher.GetJsonAsync<ZaloDetailView>($"/api/zalo/classes/{classId}/broadcasts/{created.Data.Broadcast.Id}");
        detail.Data!.Broadcast.Should().Match<ZaloBroadcastView>(x => x.Sent == 1 && x.Pending == 0);
        detail.Data.Messages.Single().Status.Should().Be("SENT");

        // Gửi lại báo cáo cùng buổi phải xác nhận (force) để không làm phụ huynh nhận trùng
        var (again, againBody) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{classId}/broadcasts", request);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        againBody.Errors.Single().Code.Should().Be("ZALO_ALREADY_SENT");
        var (forced, _) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{classId}/broadcasts", new { kind = "DAILY_REPORT", sessionDate = Today, force = true });
        forced.StatusCode.Should().Be(HttpStatusCode.Created);

        var (_, list) = await teacher.GetJsonAsync<List<ZaloBroadcastView>>($"/api/zalo/classes/{classId}/broadcasts");
        list.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Failed_message_stays_failed_until_teacher_retries()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, a, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, a);
        var phone = RandomPhone();
        var intl = "84" + phone[1..];
        await SetContactAsync(teacher, classId, a, "Cô Hoa", phone);
        factory.ZaloSender.FailingPhones[intl] = true;

        var (_, created) = await teacher.PostJsonAsync<ZaloCreatedView>(
            $"/api/zalo/classes/{classId}/broadcasts", new { kind = "ANNOUNCEMENT", content = "Thứ 7 học bù lúc 18h" });
        await DispatchAsync();
        await DispatchAsync(); // lượt thứ hai không được tự gửi lại tin đã lỗi

        var url = $"/api/zalo/classes/{classId}/broadcasts/{created.Data!.Broadcast.Id}";
        var (_, failed) = await teacher.GetJsonAsync<ZaloDetailView>(url);
        failed.Data!.Broadcast.Failed.Should().Be(1);
        failed.Data.Messages.Single().Should().Match<ZaloMessageItem>(m => m.Status == "FAILED" && m.ErrorCode == "ZNS_118");
        factory.ZaloSender.Sent.Should().NotContain(s => s.Phone == intl);

        factory.ZaloSender.FailingPhones.TryRemove(intl, out _);
        var (retry, _) = await teacher.PostJsonAsync<ZaloBroadcastView>($"{url}/retry");
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        await DispatchAsync();

        var (_, ok) = await teacher.GetJsonAsync<ZaloDetailView>(url);
        ok.Data!.Messages.Single().Status.Should().Be("SENT");
        factory.ZaloSender.Sent.Count(s => s.Phone == intl).Should().Be(1);

        var (retryAgain, retryBody) = await teacher.PostJsonAsync<object>($"{url}/retry");
        retryAgain.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "không còn tin lỗi");
        retryBody.Errors.Single().Code.Should().Be("ZALO_NO_RECIPIENTS");
    }

    [Fact]
    public async Task Announcement_sends_once_per_shared_phone_and_can_be_cancelled()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, a, _) = await kit.CreateStudentAsync();
        var (_, b, _) = await kit.CreateStudentAsync();
        var (_, c, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, a, b, c);
        var shared = RandomPhone();
        var own = RandomPhone();
        await SetContactAsync(teacher, classId, a, "Anh Tùng", shared);
        await SetContactAsync(teacher, classId, b, "Anh Tùng", shared);
        await SetContactAsync(teacher, classId, c, "Chị Mai", own);

        var (_, created) = await teacher.PostJsonAsync<ZaloCreatedView>(
            $"/api/zalo/classes/{classId}/broadcasts", new { kind = "ANNOUNCEMENT", content = "Nghỉ học thứ 2 do mưa bão" });
        created.Data!.Broadcast.Total.Should().Be(2);
        created.Data.Skipped.Should().ContainSingle().Which.SkipReason.Should().Be("DUPLICATE_PHONE");

        var (cancel, cancelled) = await teacher.PostJsonAsync<ZaloBroadcastView>($"/api/zalo/classes/{classId}/broadcasts/{created.Data.Broadcast.Id}/cancel");
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        cancelled.Data.Should().Match<ZaloBroadcastView>(x => x.Cancelled == 2 && x.Pending == 0);

        await DispatchAsync();
        var sharedIntl = "84" + shared[1..];
        var ownIntl = "84" + own[1..];
        factory.ZaloSender.Sent.Should().NotContain(s => s.Phone == sharedIntl || s.Phone == ownIntl);
    }

    [Fact]
    public async Task Rejects_empty_announcement_and_class_without_phones()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var (teacher, teacherId) = await CreateTeacherAsync(kit);
        var (_, a, _) = await kit.CreateStudentAsync();
        var classId = await CreateClassAsync(kit, teacherId, a);

        var (empty, _) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{classId}/broadcasts", new { kind = "ANNOUNCEMENT", content = "  " });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (noPhone, body) = await teacher.PostJsonAsync<object>($"/api/zalo/classes/{classId}/broadcasts", new { kind = "ANNOUNCEMENT", content = "Xin chào" });
        noPhone.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        body.Errors.Single().Code.Should().Be("ZALO_NO_RECIPIENTS");

        var (_, status) = await teacher.GetJsonAsync<ZaloStatusView>("/api/zalo/status");
        status.Data!.Enabled.Should().BeTrue();
    }

    private sealed record ZaloStatusView(bool Enabled, string Provider);

    private static string? Reason(List<ZaloRecipientItem> items, Guid studentId) => items.Single(r => r.StudentId == studentId).SkipReason;

    private static string RandomPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private async Task DispatchAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IZaloDispatchService>().DispatchPendingAsync(CancellationToken.None);
    }

    private static async Task SetContactAsync(HttpClient teacher, Guid classId, Guid studentId, string parentName, string phone)
    {
        var (response, _) = await teacher.SendJsonAsync<object>(
            HttpMethod.Put, $"/api/attendance/classes/{classId}/students/{studentId}/contact", new { parentName, parentPhone = phone });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
            code = ApiClientExtensions.UniqueName("ZL-"),
            name = "Lớp gửi Zalo",
            homeroomTeacherId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(",", body.Errors.Select(e => e.Code)));
        await kit.Admin.PostJsonAsync<ClassItem>($"/api/classes/{body.Data!.Id}/students", new { userIds = studentIds });
        return body.Data.Id;
    }
}

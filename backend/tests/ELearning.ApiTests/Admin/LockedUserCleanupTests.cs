using System.Net;
using ELearning.ApiTests.Attempts;
using ELearning.ApiTests.Videos;
using ELearning.Application.Users;
using ELearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.ApiTests.Admin;

/// <summary>Tự động xóa cứng tài khoản khóa quá 90 ngày (D-41).</summary>
[Collection(ApiCollection.Name)]
public class LockedUserCleanupTests(ApiFactory factory)
{
    [Fact]
    public async Task Accounts_locked_over_90_days_are_hard_deleted_with_their_attempts_but_content_authors_are_kept()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync();
        var (student, studentId, _) = await kit.CreateStudentAsync();
        var (_, recentId, _) = await kit.CreateStudentAsync();
        var (_, authorId, _) = await kit.CreateStudentAsync();
        var attempt = await AttemptTestKit.StartAsync(student, exam.Id);
        await AttemptTestKit.SaveAllCorrectAsync(student, attempt);
        await student.PostJsonAsync<object>($"/api/student/attempts/{attempt.AttemptId}/submit");
        await student.PostJsonAsync<object>(
            $"/api/student/attempts/{attempt.AttemptId}/question-reports", new { questionOrder = 1, reason = "TYPO" });
        var (_, classroom) = await kit.Admin.PostJsonAsync<ClassItemRef>(
            "/api/classes", new { code = ApiClientExtensions.UniqueName("CL-"), name = "Lớp dọn tài khoản" });
        await kit.Admin.PostJsonAsync<object>(
            "/api/users/bulk-status", new { userIds = new[] { studentId, recentId, authorId }, isActive = false, reason = "Nghỉ học" });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ELearningDbContext>();
            var classroomId = classroom.Data!.Id;
            // Người khóa lâu là tác giả một lớp học (nội dung chung) thì không được xóa
            await db.Database.ExecuteSqlAsync($"""UPDATE "Classrooms" SET "CreatedBy" = {authorId} WHERE "Id" = {classroomId}""");
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE "Users" SET "DeactivatedAt" = now() - interval '100 days' WHERE "Id" IN ({studentId}, {authorId})""");
            await db.Database.ExecuteSqlAsync($"""UPDATE "Users" SET "DeactivatedAt" = now() - interval '80 days' WHERE "Id" = {recentId}""");
        }

        LockedUserCleanupResult result;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<ILockedUserCleanupService>().DeleteExpiredAsync(CancellationToken.None);
        }

        var (deleted, _) = await kit.Admin.GetJsonAsync<UserDetail>($"/api/users/{studentId}");
        var (recent, _) = await kit.Admin.GetJsonAsync<UserDetail>($"/api/users/{recentId}");
        var (author, _) = await kit.Admin.GetJsonAsync<UserDetail>($"/api/users/{authorId}");
        await using var check = factory.Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ELearningDbContext>();

        result.Deleted.Should().BeGreaterThanOrEqualTo(1);
        result.Kept.Should().BeGreaterThanOrEqualTo(1);
        deleted.StatusCode.Should().Be(HttpStatusCode.NotFound, "khóa quá 90 ngày thì bị xóa cứng");
        (await checkDb.ExamAttempts.CountAsync(a => a.UserId == studentId)).Should().Be(0);
        (await checkDb.ExamResults.CountAsync(r => r.UserId == studentId)).Should().Be(0);
        (await checkDb.QuestionReports.CountAsync(r => r.ReportedBy == studentId)).Should().Be(0);
        (await checkDb.AuditLogs.CountAsync(l => l.EntityId == studentId && l.Action == "USER_DELETED")).Should().Be(1);
        recent.StatusCode.Should().Be(HttpStatusCode.OK, "khóa chưa đủ 90 ngày");
        author.StatusCode.Should().Be(HttpStatusCode.OK, "tác giả nội dung được giữ lại");
    }
}

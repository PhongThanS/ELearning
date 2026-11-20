using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Users;

public interface ILockedUserCleanupService
{
    /// <summary>
    /// Xóa cứng các tài khoản đã khóa quá <see cref="UserRetentionOptions.DeleteLockedAfterDays"/> ngày (D-41), mỗi lần tối đa
    /// <see cref="UserRetentionOptions.BatchSize"/> tài khoản.
    /// </summary>
    Task<LockedUserCleanupResult> DeleteExpiredAsync(CancellationToken ct);
}

/// <summary>Deleted: số tài khoản đã xóa; Kept: tài khoản quá hạn nhưng là tác giả nội dung nên giữ lại.</summary>
public sealed record LockedUserCleanupResult(int Deleted, int Kept);

/// <summary>
/// Tự động xóa cứng tài khoản bị khóa lâu (D-41, thay D-16 cho riêng trường hợp này). Xóa luôn dữ liệu của chính tài khoản:
/// lượt thi, câu trả lời, kết quả, lịch sử điểm, điểm danh, liên hệ, thành viên lớp / nhóm, gán đề, vai trò, phiên đăng nhập.
/// Các cột "người sửa / người hủy / người chấm" của dữ liệu khác trỏ tới tài khoản thì đặt NULL.
/// Tài khoản là tác giả nội dung (lớp, đề, câu hỏi, chuyên đề, video, buổi điểm danh, ảnh, nhóm, sửa đáp án,
/// gán đề cho người khác) thì KHÔNG xóa để không mất nội dung chung; ghi cảnh báo để quản trị xử lý tay.
/// Nhật ký hệ thống (AuditLogs) giữ nguyên, có thêm một dòng USER_DELETED.
/// </summary>
internal sealed class LockedUserCleanupService(
    IAppDbContext db,
    IAuditService audit,
    TimeProvider time,
    IOptions<UserRetentionOptions> options,
    ILogger<LockedUserCleanupService> logger) : ILockedUserCleanupService
{
    public async Task<LockedUserCleanupResult> DeleteExpiredAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (settings.DeleteLockedAfterDays <= 0)
        {
            return new LockedUserCleanupResult(0, 0);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-settings.DeleteLockedAfterDays);
        var candidates = await db.Users.AsNoTracking()
            .Where(u => !u.IsActive && u.DeactivatedAt != null && u.DeactivatedAt < cutoff)
            .OrderBy(u => u.DeactivatedAt)
            .Take(settings.BatchSize)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.Email, u.DeactivatedAt })
            .ToListAsync(ct);

        var deleted = 0;
        var kept = 0;
        foreach (var user in candidates)
        {
            if (await IsContentAuthorAsync(user.Id, ct))
            {
                kept++;
                logger.LogWarning(
                    "Tài khoản {UserName} ({UserId}) đã khóa quá {Days} ngày nhưng là tác giả nội dung nên không tự xóa",
                    user.UserName, user.Id, settings.DeleteLockedAfterDays);
                continue;
            }

            var removed = await db.InTransactionAsync(
                async () =>
                {
                    var ok = await HardDeleteAsync(user.Id, ct);
                    if (ok)
                    {
                        audit.Write(
                            AuditActions.UserDeleted,
                            nameof(User),
                            user.Id,
                            new { user.UserName, user.FullName, user.Email, user.DeactivatedAt },
                            reason: $"Tự động xóa: khóa quá {settings.DeleteLockedAfterDays} ngày");
                        await db.SaveChangesAsync(ct);
                    }

                    return ok;
                },
                ct);
            if (removed)
            {
                deleted++;
                logger.LogInformation("Đã tự động xóa tài khoản {UserName} ({UserId}) khóa từ {DeactivatedAt}", user.UserName, user.Id, user.DeactivatedAt);
            }
        }

        return new LockedUserCleanupResult(deleted, kept);
    }

    /// <summary>Tài khoản đã tạo nội dung dùng chung (cột NOT NULL trỏ tới tài khoản ngoài dữ liệu của chính họ).</summary>
    private async Task<bool> IsContentAuthorAsync(Guid u, CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS (SELECT 1 FROM "Classrooms" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "Exams" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "ExamVersions" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "Questions" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "QuestionCategories" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "MediaFiles" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "UserGroups" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "VideoLessons" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "AttendanceSessions" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "ZaloBroadcasts" WHERE "CreatedBy" = {u})
                OR EXISTS (SELECT 1 FROM "AnswerKeyCorrections" WHERE "CorrectedBy" = {u})
                OR EXISTS (SELECT 1 FROM "ExamAssignments" WHERE "CreatedBy" = {u} AND ("UserId" IS NULL OR "UserId" <> {u}))
                OR EXISTS (SELECT 1 FROM "ExamUserOverrides" WHERE "UpdatedBy" = {u} AND "UserId" <> {u})
            ) AS "Value"
            """).SingleAsync(ct);

    /// <summary>Xóa dữ liệu của tài khoản theo thứ tự khóa ngoại (mọi FK là Restrict), rồi xóa tài khoản. Chạy trong transaction.</summary>
    private async Task<bool> HardDeleteAsync(Guid u, CancellationToken ct)
    {
        var sql = db.Database;

        // Khóa dòng tài khoản và kiểm tra lại: quản trị vừa mở khóa thì thôi, không xóa gì
        var stillLocked = await sql.SqlQuery<bool>($"""SELECT NOT "IsActive" AS "Value" FROM "Users" WHERE "Id" = {u} FOR UPDATE""")
            .ToListAsync(ct);
        if (stillLocked is not [true])
        {
            return false;
        }

        await sql.ExecuteSqlAsync($"""
            DELETE FROM "AttemptAnswerOptions" WHERE "AttemptAnswerId" IN (
                SELECT aa."Id" FROM "AttemptAnswers" aa
                JOIN "AttemptQuestions" aq ON aq."Id" = aa."AttemptQuestionId"
                JOIN "ExamAttempts" a ON a."Id" = aq."AttemptId"
                WHERE a."UserId" = {u})
            """, ct);
        await sql.ExecuteSqlAsync($"""
            DELETE FROM "AttemptAnswers" WHERE "AttemptQuestionId" IN (
                SELECT aq."Id" FROM "AttemptQuestions" aq JOIN "ExamAttempts" a ON a."Id" = aq."AttemptId" WHERE a."UserId" = {u})
            """, ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "AttemptQuestions" WHERE "AttemptId" IN (SELECT "Id" FROM "ExamAttempts" WHERE "UserId" = {u})""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "AttemptEvents" WHERE "AttemptId" IN (SELECT "Id" FROM "ExamAttempts" WHERE "UserId" = {u})""", ct);
        await sql.ExecuteSqlAsync($"""
            DELETE FROM "ExamResultHistory" WHERE "ExamResultId" IN (
                SELECT "Id" FROM "ExamResults"
                WHERE "UserId" = {u} OR "AttemptId" IN (SELECT "Id" FROM "ExamAttempts" WHERE "UserId" = {u}))
            """, ct);
        await sql.ExecuteSqlAsync($"""
            DELETE FROM "ExamResults" WHERE "UserId" = {u} OR "AttemptId" IN (SELECT "Id" FROM "ExamAttempts" WHERE "UserId" = {u})
            """, ct);

        // Báo lỗi câu hỏi (D-46) của chính học sinh, hoặc gắn với lượt thi của học sinh
        await sql.ExecuteSqlAsync($"""
            DELETE FROM "QuestionReports" WHERE "ReportedBy" = {u} OR "AttemptId" IN (SELECT "Id" FROM "ExamAttempts" WHERE "UserId" = {u})
            """, ct);
        await sql.ExecuteSqlAsync($"""UPDATE "QuestionReports" SET "ResolvedBy" = NULL WHERE "ResolvedBy" = {u}""", ct);

        // Cột tham chiếu cho phép NULL (người sửa / hủy / chấm / chủ nhiệm) ở dữ liệu còn lại
        await sql.ExecuteSqlAsync($"""UPDATE "ExamAttempts" SET "CancelledBy" = NULL WHERE "CancelledBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "AttemptAnswers" SET "ManualGradedBy" = NULL WHERE "ManualGradedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "ExamQuestions" SET "VoidedBy" = NULL WHERE "VoidedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "ExamVersions" SET "PublishedBy" = NULL WHERE "PublishedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "Classrooms" SET "HomeroomTeacherId" = NULL WHERE "HomeroomTeacherId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "Classrooms" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "Exams" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "Questions" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "VideoLessons" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "AttendanceSessions" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "AttendanceRecords" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""UPDATE "StudentContacts" SET "UpdatedBy" = NULL WHERE "UpdatedBy" = {u}""", ct);

        await sql.ExecuteSqlAsync($"""DELETE FROM "ExamAttempts" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "AttendanceRecords" WHERE "StudentId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "ZaloMessages" WHERE "StudentId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "StudentContacts" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "ClassroomStudents" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "UserGroupMembers" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "ExamAssignments" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "ExamUserOverrides" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "UserRoles" WHERE "UserId" = {u}""", ct);
        await sql.ExecuteSqlAsync($"""DELETE FROM "RefreshTokens" WHERE "UserId" = {u}""", ct);

        // Chỉ xóa nếu vẫn đang khóa (quản trị có thể vừa mở khóa)
        return await sql.ExecuteSqlAsync($"""DELETE FROM "Users" WHERE "Id" = {u} AND "IsActive" = FALSE""", ct) == 1;
    }
}

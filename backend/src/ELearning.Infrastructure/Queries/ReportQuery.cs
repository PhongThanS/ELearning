using Dapper;
using ELearning.Application.Admin;
using ELearning.Domain.Enums;
using ELearning.Infrastructure.Persistence;
using ELearning.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Infrastructure.Queries;

/// <summary>
/// Báo cáo bằng Dapper (PostgreSQL).
/// </summary>
internal sealed class ReportQuery(ELearningDbContext db) : IReportQuery
{
    private const string FinishedStatuses = "('SUBMITTED','AUTO_SUBMITTED')";

    public async Task<DashboardDto> GetDashboardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime nowUtc, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var parameters = new { start = todayStartUtc, end = todayEndUtc, now = nowUtc, since = nowUtc.AddDays(-30) };

        var counters = await connection.QuerySingleAsync<DashboardCounters>(new CommandDefinition(
            $"""
            SELECT
                (SELECT COUNT(*)::int FROM "Users" WHERE "AnonymizedAt" IS NULL) AS TotalUsers,
                (SELECT COUNT(DISTINCT ur."UserId")::int FROM "UserRoles" ur JOIN "Roles" r ON r."Id" = ur."RoleId"
                    JOIN "Users" u ON u."Id" = ur."UserId" WHERE r."Code" = 'STUDENT' AND u."IsActive" = TRUE) AS TotalStudents,
                (SELECT COUNT(*)::int FROM "Exams") AS TotalExams,
                (SELECT COUNT(*)::int FROM "Exams" WHERE "Status" = 'PUBLISHED'
                    AND ("StartAt" IS NULL OR "StartAt" <= @now) AND ("EndAt" IS NULL OR "EndAt" > @now)) AS OpenExams,
                (SELECT COUNT(*)::int FROM "ExamAttempts" WHERE "StartedAt" >= @start AND "StartedAt" < @end) AS AttemptsToday,
                (SELECT COUNT(*)::int FROM "ExamAttempts" WHERE "Status" = 'IN_PROGRESS') AS InProgressAttempts,
                (SELECT CAST(AVG(r."Percentage") AS decimal(5,2)) FROM "ExamResults" r
                    JOIN "ExamAttempts" a ON a."Id" = r."AttemptId"
                    WHERE r."SubmittedAt" >= @since AND a."Status" IN {FinishedStatuses}) AS AveragePercentage30Days,
                (SELECT CAST(100.0 * SUM(CASE WHEN r."Passed" = TRUE THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) AS decimal(5,2))
                    FROM "ExamResults" r JOIN "ExamAttempts" a ON a."Id" = r."AttemptId"
                    WHERE r."SubmittedAt" >= @since AND r."Passed" IS NOT NULL AND a."Status" IN {FinishedStatuses}) AS PassRate30Days
            """,
            parameters,
            cancellationToken: ct));

        var upcoming = (await connection.QueryAsync<UpcomingRow>(new CommandDefinition(
            """
            SELECT e."Id" AS ExamId, e."Code", e."Name", e."StartAt", e."EndAt",
                (SELECT COUNT(*)::int FROM "ExamAttempts" a WHERE a."ExamId" = e."Id" AND a."Status" = 'IN_PROGRESS') AS InProgressAttempts
            FROM "Exams" e
            WHERE e."Status" = 'PUBLISHED' AND (e."EndAt" IS NULL OR e."EndAt" > @now)
            ORDER BY CASE WHEN e."StartAt" IS NULL THEN 1 ELSE 0 END, e."StartAt", e."Name"
            LIMIT 10
            """,
            parameters,
            cancellationToken: ct))).ToList();

        return new DashboardDto(
            counters.TotalUsers,
            counters.TotalStudents,
            counters.TotalExams,
            counters.OpenExams,
            counters.AttemptsToday,
            counters.InProgressAttempts,
            counters.AveragePercentage30Days,
            counters.PassRate30Days,
            upcoming.Select(u => new UpcomingExamDto(u.ExamId, u.Code, u.Name, Utc(u.StartAt), Utc(u.EndAt), u.InProgressAttempts)).ToList());
    }

    public async Task<IReadOnlyList<QuestionStatDto>> GetQuestionStatisticsAsync(Guid versionId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var rows = (await connection.QueryAsync<QuestionStatRow>(new CommandDefinition(
            $"""
            SELECT eq."Id" AS ExamQuestionId, eq."QuestionOrder" AS "Order", LEFT(eq."Content", 200) AS ContentPreview,
                eq."QuestionType", eq."Score" AS MaxScore, eq."IsVoided",
                COUNT(aa."Id")::int AS AttemptCount,
                COALESCE(SUM(CASE WHEN aa."IsAnswered" = TRUE THEN 1 ELSE 0 END), 0)::int AS AnsweredCount,
                COALESCE(SUM(CASE WHEN aa."IsCorrect" = TRUE THEN 1 ELSE 0 END), 0)::int AS CorrectCount,
                COALESCE(SUM(CASE WHEN aa."IsAnswered" = TRUE AND aa."IsCorrect" = FALSE THEN 1 ELSE 0 END), 0)::int AS WrongCount,
                COALESCE(SUM(CASE WHEN aa."Id" IS NOT NULL AND aa."IsAnswered" = FALSE THEN 1 ELSE 0 END), 0)::int AS BlankCount,
                CAST(AVG(aa."Score") AS decimal(10,2)) AS AverageScore
            FROM "ExamQuestions" eq
            LEFT JOIN "AttemptQuestions" aq ON aq."ExamQuestionId" = eq."Id"
            LEFT JOIN "ExamAttempts" a ON a."Id" = aq."AttemptId" AND a."Status" IN {FinishedStatuses}
            LEFT JOIN "AttemptAnswers" aa ON aa."AttemptQuestionId" = aq."Id" AND a."Id" IS NOT NULL
            WHERE eq."ExamVersionId" = @versionId
            GROUP BY eq."Id", eq."QuestionOrder", LEFT(eq."Content", 200), eq."QuestionType", eq."Score", eq."IsVoided"
            ORDER BY eq."QuestionOrder"
            """,
            new { versionId },
            cancellationToken: ct))).ToList();

        var options = (await connection.QueryAsync<OptionRow>(new CommandDefinition(
            $"""
            SELECT o."ExamQuestionId", o."OptionCode", o."DisplayOrder",
                (SELECT COUNT(*)::int FROM "AttemptAnswerOptions" aao
                    JOIN "AttemptAnswers" aa ON aa."Id" = aao."AttemptAnswerId"
                    JOIN "AttemptQuestions" aq ON aq."Id" = aa."AttemptQuestionId"
                    JOIN "ExamAttempts" a ON a."Id" = aq."AttemptId"
                    WHERE aq."ExamQuestionId" = o."ExamQuestionId" AND aao."OptionCode" = o."OptionCode"
                        AND a."Status" IN {FinishedStatuses}) AS SelectedCount
            FROM "ExamQuestionOptions" o
            JOIN "ExamQuestions" eq ON eq."Id" = o."ExamQuestionId"
            WHERE eq."ExamVersionId" = @versionId
            """,
            new { versionId },
            cancellationToken: ct))).ToLookup(o => o.ExamQuestionId);

        return rows.Select(r => new QuestionStatDto(
            r.ExamQuestionId,
            r.Order,
            r.ContentPreview,
            EnumNaming.FromDb<QuestionType>(r.QuestionType),
            r.AttemptCount,
            r.AnsweredCount,
            r.CorrectCount,
            r.WrongCount,
            r.BlankCount,
            r.AttemptCount == 0 ? null : Math.Round(100m * r.CorrectCount / r.AttemptCount, 2, MidpointRounding.AwayFromZero),
            r.AverageScore,
            r.MaxScore,
            r.IsVoided,
            options[r.ExamQuestionId].OrderBy(o => o.DisplayOrder).Select(o => new OptionStatDto(o.OptionCode, o.SelectedCount)).ToList()))
            .ToList();
    }

    private static DateTime? Utc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private sealed record DashboardCounters(
        int TotalUsers, int TotalStudents, int TotalExams, int OpenExams, int AttemptsToday, int InProgressAttempts,
        decimal? AveragePercentage30Days, decimal? PassRate30Days);

    private sealed record UpcomingRow(Guid ExamId, string Code, string Name, DateTime? StartAt, DateTime? EndAt, int InProgressAttempts);

    private sealed record QuestionStatRow(
        Guid ExamQuestionId, int Order, string ContentPreview, string QuestionType, decimal MaxScore, bool IsVoided,
        int AttemptCount, int AnsweredCount, int CorrectCount, int WrongCount, int BlankCount, decimal? AverageScore);

    private sealed record OptionRow(Guid ExamQuestionId, string OptionCode, int DisplayOrder, int SelectedCount);
}

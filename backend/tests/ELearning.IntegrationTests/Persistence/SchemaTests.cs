using ELearning.Domain.Audit;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using ELearning.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ELearning.IntegrationTests.Persistence;

/// <summary>Kiểm tra schema thật trên PostgreSQL: migration, UTC, enum, ràng buộc (docs/08-kiem-thu.md mục 4).</summary>
[Collection(DatabaseCollection.Name)]
public class SchemaTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task Migrations_are_fully_applied_and_model_has_no_pending_changes()
    {
        await using var context = database.CreateContext();

        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse("mọi thay đổi model phải có migration");
    }

    [Fact]
    public async Task No_foreign_key_uses_cascade_delete()
    {
        await using var context = database.CreateContext();

        var cascading = await context.Database
            .SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.referential_constraints
                WHERE delete_rule NOT IN ('NO ACTION', 'RESTRICT')
                """)
            .SingleAsync();

        cascading.Should().Be(0);
    }

    [Fact]
    public async Task DateTime_read_back_is_utc()
    {
        var createdAt = new DateTime(2026, 9, 26, 3, 15, 30, 123, DateTimeKind.Utc);
        long id;
        await using (var context = database.CreateContext())
        {
            var log = new AuditLog("TEST_UTC", null, null, null, null, null, null, null, null, null, createdAt);
            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
            id = log.Id;
        }

        await using (var context = database.CreateContext())
        {
            var loaded = await context.AuditLogs.SingleAsync(a => a.Id == id);
            loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
            loaded.CreatedAt.Should().Be(createdAt);
        }
    }

    [Fact]
    public async Task Enum_columns_store_upper_snake_case_and_are_read_back()
    {
        var user = await CreateUserAsync();
        var examId = Guid.NewGuid();

        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Exams" ("Id", "Code", "Name", "Status", "MaxAttempts", "AccessMode", "RetakeScoringPolicy", "CreatedBy", "CreatedAt", "RowVersion")
                VALUES ({examId}, {"EX-" + examId.ToString("N")}, 'Đề thử', 'PUBLISHED', 1, 'ASSIGNED', 'HIGHEST', {user.Id}, CURRENT_TIMESTAMP, {Guid.NewGuid().ToByteArray()})
                """);
        }

        await using (var context = database.CreateContext())
        {
            var exam = await context.Exams.AsNoTracking().SingleAsync(e => e.Id == examId);
            exam.Status.Should().Be(ExamStatus.Published);
            exam.AccessMode.Should().Be(AccessMode.Assigned);
            exam.RetakeScoringPolicy.Should().Be(RetakeScoringPolicy.Highest);
        }
    }

    [Fact]
    public async Task Check_constraint_rejects_unknown_enum_value()
    {
        var user = await CreateUserAsync();
        await using var context = database.CreateContext();

        var act = () => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Exams" ("Id", "Code", "Name", "Status", "MaxAttempts", "AccessMode", "RetakeScoringPolicy", "CreatedBy", "CreatedAt", "RowVersion")
            VALUES ({Guid.NewGuid()}, {"EX-" + Guid.NewGuid().ToString("N")}, 'Sai', 'Published', 1, 'ASSIGNED', 'HIGHEST', {user.Id}, CURRENT_TIMESTAMP, {Guid.NewGuid().ToByteArray()})
            """);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Only_one_published_version_per_exam_is_allowed()
    {
        var user = await CreateUserAsync();
        var examId = Guid.NewGuid();
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Exams" ("Id", "Code", "Name", "Status", "MaxAttempts", "AccessMode", "RetakeScoringPolicy", "CreatedBy", "CreatedAt", "RowVersion")
            VALUES ({examId}, {"EX-" + examId.ToString("N")}, 'Đề', 'PUBLISHED', 1, 'PUBLIC', 'HIGHEST', {user.Id}, CURRENT_TIMESTAMP, {Guid.NewGuid().ToByteArray()});
            INSERT INTO "ExamVersions" ("Id", "ExamId", "VersionNumber", "Status", "DurationMinutes", "ScoreVisibility", "ReviewPolicy",
                                      "ShuffleQuestions", "ShuffleOptions", "CreatedBy", "CreatedAt", "RowVersion")
            VALUES (gen_random_uuid(), {examId}, 1, 'PUBLISHED', 60, 'IMMEDIATE', 'NEVER', FALSE, FALSE, {user.Id}, CURRENT_TIMESTAMP, {Guid.NewGuid().ToByteArray()});
            """);

        var secondPublished = () => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ExamVersions" ("Id", "ExamId", "VersionNumber", "Status", "DurationMinutes", "ScoreVisibility", "ReviewPolicy",
                                      "ShuffleQuestions", "ShuffleOptions", "CreatedBy", "CreatedAt", "RowVersion")
            VALUES (gen_random_uuid(), {examId}, 2, 'PUBLISHED', 60, 'IMMEDIATE', 'NEVER', FALSE, FALSE, {user.Id}, CURRENT_TIMESTAMP, {Guid.NewGuid().ToByteArray()})
            """);

        (await secondPublished.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Normalized_user_name_is_unique()
    {
        var first = await CreateUserAsync();
        await using var context = database.CreateContext();
        context.Users.Add(new User(first.UserName.ToUpperInvariant(), $"{Guid.NewGuid():N}@test.vn", "Trùng tên", DateTime.UtcNow));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<User> CreateUserAsync()
    {
        await using var context = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new User($"user_{suffix}", $"{suffix}@test.vn", "Người dùng thử", DateTime.UtcNow);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}

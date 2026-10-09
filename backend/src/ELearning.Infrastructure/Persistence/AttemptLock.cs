using ELearning.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ELearning.Infrastructure.Persistence;

/// <summary>Khóa dòng lượt thi bằng SELECT ... FOR UPDATE (PostgreSQL, D-21, docs/10-bay-ky-thuat.md mục 6).</summary>
internal sealed class AttemptLock(ELearningDbContext db) : IAttemptLock
{
    public async Task LockAsync(Guid attemptId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Khóa lượt thi phải được gọi bên trong transaction.");
        }

        // PostgreSQL khóa dòng bằng SELECT ... FOR UPDATE
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"ExamAttempts\" WHERE \"Id\" = {attemptId} FOR UPDATE", ct);
    }

    public bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}

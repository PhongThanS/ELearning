using ELearning.Application.Common.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Infrastructure.Persistence;

/// <summary>Khóa dòng lượt thi bằng UPDLOCK, ROWLOCK (D-21, docs/10-bay-ky-thuat.md mục 6).</summary>
internal sealed class AttemptLock(ELearningDbContext db) : IAttemptLock
{
    public async Task LockAsync(Guid attemptId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Khóa lượt thi phải được gọi bên trong transaction.");
        }

        // SELECT có hint khóa; khóa được giữ tới khi transaction kết thúc.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT TOP (1) 1 FROM [ExamAttempts] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {attemptId}", ct);
    }

    public bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };
}

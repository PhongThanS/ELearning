using System.Globalization;
using ELearning.Application.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ELearning.Infrastructure.Persistence;

/// <summary>
/// Sinh mã "Q000123" từ SEQUENCE SQL Server. Dùng ADO trực tiếp vì NEXT VALUE FOR
/// không được phép trong subquery mà SqlQueryRaw của EF sinh ra.
/// </summary>
internal sealed class CodeGenerator(ELearningDbContext db) : ICodeGenerator
{
    public async Task<string> NextQuestionCodeAsync(CancellationToken ct)
    {
        // Mã tự sinh có thể trùng mã admin tự đặt trước đó → bỏ qua và lấy số tiếp theo.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = $"Q{await NextValueAsync(ELearningDbContext.QuestionCodeSequence, ct):D6}";
            if (!await db.Questions.AnyAsync(q => q.Code == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Không sinh được mã câu hỏi duy nhất.");
    }

    private async Task<long> NextValueAsync(string sequence, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT nextval('\"{sequence}\"')";
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

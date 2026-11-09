using System.Globalization;
using System.Text.RegularExpressions;
using ELearning.Application.Questions;
using ELearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ELearning.Infrastructure.Persistence;

/// <summary>
/// Sinh mã "Q000123" từ SEQUENCE SQL Server. Dùng ADO trực tiếp vì NEXT VALUE FOR
/// không được phép trong subquery mà SqlQueryRaw của EF sinh ra.
/// </summary>
internal sealed class CodeGenerator(ELearningDbContext db) : ICodeGenerator
{
    private const int MaxSequence = 999_999;

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

    public async Task<IReadOnlyList<string>> NextCategoryQuestionCodesAsync(
        string categoryCode, IReadOnlyList<QuestionDifficulty?> difficulties, CancellationToken ct)
    {
        var prefix = CategoryPrefix(categoryCode);

        // Khóa theo chuyên đề tới hết transaction: hai lượt nhập cùng lúc không lấy trùng số thứ tự
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({"question-code:" + prefix}))", ct);

        var pattern = new Regex("^" + Regex.Escape(prefix) + @"-\d{2}-(\d{6})$", RegexOptions.CultureInvariant);
        var existing = await db.Questions.AsNoTracking()
            .Where(q => q.Code.StartsWith(prefix + "-"))
            .Select(q => q.Code)
            .ToListAsync(ct);
        var last = existing
            .Select(code => pattern.Match(code))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
        if (last + difficulties.Count > MaxSequence)
        {
            throw new InvalidOperationException($"Chuyên đề {categoryCode} đã dùng hết số thứ tự mã câu hỏi ({MaxSequence}).");
        }

        return difficulties
            .Select((difficulty, i) => string.Create(
                CultureInfo.InvariantCulture, $"{prefix}-{(int)(difficulty ?? 0):D2}-{last + i + 1:D6}"))
            .ToList();
    }

    /// <summary>Mã chuyên đề dùng làm đầu mã câu hỏi: ký tự ngoài <c>A-Z a-z 0-9 . _ -</c> (như # +) đổi thành "_".</summary>
    internal static string CategoryPrefix(string categoryCode)
    {
        var prefix = Regex.Replace(categoryCode.Trim(), "[^A-Za-z0-9._-]", "_");
        // Mã câu hỏi tối đa 100 ký tự; phần đuôi "-00-000000" chiếm 10
        return prefix.Length > 90 ? prefix[..90] : prefix;
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

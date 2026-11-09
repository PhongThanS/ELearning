using ELearning.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Questions;

/// <summary>
/// Chuyên đề 2 cấp: "thuộc chuyên đề X" nghĩa là thuộc X hoặc một chuyên đề con của X.
/// Dùng cho lọc câu hỏi / đề / video, bốc câu ngẫu nhiên và kiểm tra câu thuộc chuyên đề của đề (D-30).
/// </summary>
public static class CategoryFamily
{
    /// <summary>Id của chuyên đề và mọi chuyên đề con trực tiếp.</summary>
    public static async Task<List<Guid>> CategoryFamilyIdsAsync(this IAppDbContext db, Guid categoryId, CancellationToken ct)
    {
        var ids = await db.QuestionCategories.AsNoTracking()
            .Where(c => c.ParentId == categoryId)
            .Select(c => c.Id)
            .ToListAsync(ct);
        ids.Insert(0, categoryId);
        return ids;
    }

    /// <summary>Chuyên đề có chuyên đề con chỉ dùng để gom nhóm, không chứa câu hỏi trực tiếp (D-32).</summary>
    public const string HasChildrenMessage = "Chuyên đề này có chuyên đề con nên không chứa câu hỏi; hãy chọn một chuyên đề con.";

    public static Task<bool> CategoryHasChildrenAsync(this IAppDbContext db, Guid categoryId, CancellationToken ct) =>
        db.QuestionCategories.AnyAsync(c => c.ParentId == categoryId, ct);
}

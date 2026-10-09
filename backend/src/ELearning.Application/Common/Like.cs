namespace ELearning.Application.Common;

/// <summary>Dựng pattern LIKE an toàn: escape %, _, \ trong từ khóa.</summary>
public static class Like
{
    public static string Contains(string keyword) => $"%{Escape(keyword.Trim())}%";

    public static string Escape(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}

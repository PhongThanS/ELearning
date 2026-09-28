namespace ELearning.Shared.Paging;

/// <summary>Tham số phân trang; pageSize bị ép trong khoảng 1..100 (docs/05-api.md mục 5).</summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public string? SortBy { get; init; }

    public string? SortDir { get; init; }

    public bool SortDescending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);

    public int Skip => (Page - 1) * PageSize;
}

namespace ELearning.Shared.Results;

/// <summary>Loại lỗi, quyết định HTTP status (xem docs/05-api.md mục 3).</summary>
public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    BusinessRule,
    RateLimited,
    Unexpected,
}

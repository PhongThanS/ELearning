using ELearning.Shared;
using ELearning.Shared.Results;

namespace ELearning.Api.Common;

/// <summary>Ánh xạ loại lỗi / mã lỗi sang HTTP status (docs/05-api.md mục 3–4).</summary>
public static class ErrorMapping
{
    private static readonly HashSet<string> ConflictCodes = new(StringComparer.Ordinal)
    {
        ErrorCodes.ConcurrencyConflict,
        ErrorCodes.DuplicateCode,
        ErrorCodes.AttemptNotInProgress,
        ErrorCodes.VersionImmutable,
        ErrorCodes.ExamNotDraft,
        ErrorCodes.DraftVersionExists,
        ErrorCodes.RetakePolicyLocked,
        ErrorCodes.UserNameTaken,
        ErrorCodes.EmailTaken,
        ErrorCodes.SystemRoleImmutable,
    };

    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Loại lỗi của một DomainException, suy ra từ mã lỗi.</summary>
    public static ErrorType TypeForDomainCode(string code) =>
        ConflictCodes.Contains(code) ? ErrorType.Conflict : ErrorType.BusinessRule;
}

using ELearning.Shared.Results;

namespace ELearning.Api.Common;

public sealed record ApiError(string? Field, string Code, string Message);

/// <summary>Wrapper chung cho mọi response (D-13, docs/05-api.md mục 2).</summary>
public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, IReadOnlyList<ApiError> Errors, string? TraceId);

public static class ApiResponse
{
    public const string ValidationMessage = "Dữ liệu không hợp lệ.";

    public static ApiResponse<T> Ok<T>(T data, string? traceId) => new(true, data, null, [], traceId);

    public static ApiResponse<object> Fail(IEnumerable<Error> errors, string? traceId, string? message = null)
    {
        var list = errors.Select(e => new ApiError(e.Field, e.Code, e.Message)).ToList();
        var first = list.Count > 0 ? list[0] : null;
        return new ApiResponse<object>(false, null, message ?? first?.Message, list, traceId);
    }

    public static ApiResponse<object> Fail(Error error, string? traceId) => Fail([error], traceId);
}

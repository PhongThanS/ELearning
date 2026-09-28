using System.Diagnostics;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Common;

/// <summary>Bảo đảm cả lỗi không đi qua controller (404 route, 405, 401...) cũng trả ApiResponse.</summary>
public static class StatusCodeResponses
{
    public static async Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        if (response.HasStarted || response.ContentLength > 0 || !string.IsNullOrEmpty(response.ContentType))
        {
            return;
        }

        var error = response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized when context.HttpContext.Request.Headers.Authorization.Count > 0 =>
                new Error(ErrorType.Unauthorized, ErrorCodes.TokenInvalid, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."),
            StatusCodes.Status401Unauthorized =>
                new Error(ErrorType.Unauthorized, ErrorCodes.Unauthenticated, "Bạn cần đăng nhập để tiếp tục."),
            StatusCodes.Status403Forbidden => Error.Forbidden(),
            StatusCodes.Status404NotFound => Error.NotFound(message: "Không tìm thấy đường dẫn."),
            StatusCodes.Status405MethodNotAllowed =>
                new Error(ErrorType.Validation, ErrorCodes.MethodNotAllowed, "Phương thức HTTP không được hỗ trợ."),
            StatusCodes.Status415UnsupportedMediaType =>
                Error.Validation(ErrorCodes.ValidationFailed, "Định dạng nội dung không được hỗ trợ."),
            StatusCodes.Status429TooManyRequests =>
                new Error(ErrorType.RateLimited, ErrorCodes.RateLimited, "Bạn thao tác quá nhanh. Vui lòng thử lại sau."),
            _ => null,
        };

        if (error is null)
        {
            return;
        }

        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        await response.WriteAsJsonAsync(ApiResponse.Fail(error, traceId));
    }

    /// <summary>Lỗi model binding / [ApiController] validation → 400 ApiResponse thay vì ProblemDetails.</summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e => Error.Validation(
                ErrorCodes.InvalidValue,
                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage,
                NormalizeField(kv.Key))))
            .ToList();

        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(ApiResponse.Fail(errors, traceId, ApiResponse.ValidationMessage));
    }

    private static string? NormalizeField(string key)
    {
        var field = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.IsNullOrEmpty(field) || field == "$" ? null : char.ToLowerInvariant(field[0]) + field[1..];
    }
}

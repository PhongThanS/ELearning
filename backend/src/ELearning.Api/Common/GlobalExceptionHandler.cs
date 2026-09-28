using System.Diagnostics;
using ELearning.Domain.Common;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Api.Common;

/// <summary>
/// Chuyển exception thành ApiResponse. Không bao giờ trả stack trace (docs/07-bao-mat.md mục 1).
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client đã ngắt kết nối; không còn ai nhận response.
            return true;
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var (status, response) = exception switch
        {
            DomainException domain => Map(ErrorMapping.TypeForDomainCode(domain.Code), [new Error(ErrorType.BusinessRule, domain.Code, domain.Message)], traceId),
            DbUpdateConcurrencyException => Map(
                ErrorType.Conflict,
                [Error.Conflict(ErrorCodes.ConcurrencyConflict, "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.")],
                traceId),
            ValidationException validation => Map(
                ErrorType.Validation,
                validation.Errors.Select(e => Error.Validation(e.ErrorCode, e.ErrorMessage, ToCamelCase(e.PropertyName))).ToList(),
                traceId,
                ApiResponse.ValidationMessage),
            BadHttpRequestException => Map(
                ErrorType.Validation, [Error.Validation(ErrorCodes.ValidationFailed, "Request không hợp lệ.")], traceId),
            _ => Map(
                ErrorType.Unexpected,
                [new Error(ErrorType.Unexpected, ErrorCodes.InternalError, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")],
                traceId),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with {StatusCode}: {ExceptionType}", status, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static (int Status, ApiResponse<object> Response) Map(
        ErrorType type, IReadOnlyList<Error> errors, string traceId, string? message = null) =>
        (ErrorMapping.ToStatusCode(type), ApiResponse.Fail(errors, traceId, message));

    private static string? ToCamelCase(string? name) =>
        string.IsNullOrEmpty(name) ? null : char.ToLowerInvariant(name[0]) + name[1..];
}

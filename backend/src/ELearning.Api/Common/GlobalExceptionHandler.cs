using System.Data.Common;
using System.Diagnostics;
using ELearning.Application.Monitoring;
using ELearning.Domain.Common;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ELearning.Api.Common;

/// <summary>
/// Chuyển exception thành ApiResponse. Không bao giờ trả stack trace (docs/07-bao-mat.md mục 1).
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, OperationalMetrics metrics) : IExceptionHandler
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
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => Map(
                ErrorType.Conflict,
                [Error.Conflict(ErrorCodes.DuplicateCode, "Dữ liệu bị trùng với bản ghi đã có.")],
                traceId),
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
            if (IsDatabaseError(exception))
            {
                metrics.RecordDatabaseError();
            }

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

    /// <summary>Lỗi phía SQL Server (timeout, mất kết nối, deadlock hết lượt retry…), cho chỉ số "số lỗi DB".</summary>
    private static bool IsDatabaseError(Exception exception) =>
        exception is DbException or DbUpdateException or RetryLimitExceededException
        || exception.InnerException is DbException;

    private static string? ToCamelCase(string? name) =>
        string.IsNullOrEmpty(name) ? null : char.ToLowerInvariant(name[0]) + name[1..];
}

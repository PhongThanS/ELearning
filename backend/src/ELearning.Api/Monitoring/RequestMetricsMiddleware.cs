using ELearning.Application.Monitoring;

namespace ELearning.Api.Monitoring;

/// <summary>
/// Đếm request và response 5xx cho cảnh báo tỉ lệ lỗi. Đặt ngoài cùng (trước UseExceptionHandler) để thấy status cuối.
/// Không đếm /health/* để việc giám sát tự gọi không làm loãng tỉ lệ.
/// </summary>
internal sealed class RequestMetricsMiddleware(RequestDelegate next, OperationalMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch
        {
            metrics.RecordRequest(StatusCodes.Status500InternalServerError);
            throw;
        }

        metrics.RecordRequest(context.Response.StatusCode);
    }
}

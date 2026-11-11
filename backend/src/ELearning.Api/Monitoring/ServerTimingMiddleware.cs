using System.Globalization;
using Serilog.Events;

namespace ELearning.Api.Monitoring;

/// <summary>
/// Ghi thời gian API tự xử lý vào header <c>Server-Timing: app;dur=…</c> (ms). Trình duyệt (DevTools → Timing) và
/// <c>performance.getEntriesByType("resource")[i].serverTiming</c> đọc được, nên khi thấy chậm tách được phần của máy
/// chủ với phần của đường mạng (Cloudflare, tunnel) — docs/09-van-hanh.md mục 7.
/// </summary>
internal sealed class ServerTimingMiddleware(RequestDelegate next, TimeProvider time)
{
    public const string HeaderName = "Server-Timing";

    public Task InvokeAsync(HttpContext context)
    {
        var startedAt = time.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var elapsed = time.GetElapsedTime(startedAt).TotalMilliseconds;
            context.Response.Headers[HeaderName] = "app;dur=" + elapsed.ToString("0.0", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });
        return next(context);
    }
}

/// <summary>Mức log của dòng "HTTP … responded … in … ms" (UseSerilogRequestLogging).</summary>
internal static class RequestLogLevel
{
    /// <summary>Request xử lý lâu hơn ngưỡng này ghi mức Warning để lọc nhanh trong log.</summary>
    public const double SlowRequestMilliseconds = 500;

    public static LogEventLevel Get(HttpContext context, double elapsedMilliseconds, Exception? exception) =>
        exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : elapsedMilliseconds > SlowRequestMilliseconds
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
}

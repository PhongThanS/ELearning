using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ELearning.Api.Common;

/// <summary>Response JSON gọn cho /health/*; không lộ chi tiết exception.</summary>
public static class HealthCheckResponseWriter
{
    public const string ReadyTag = "ready";

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (int)e.Value.Duration.TotalMilliseconds,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}

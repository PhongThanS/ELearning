using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ELearning.Api.Common;

/// <summary>Response JSON gọn cho /health/*; không lộ chi tiết exception.</summary>
public static class HealthCheckResponseWriter
{
    public const string ReadyTag = "ready";

    /// <summary>Check thuộc /health/alerts (cảnh báo tối thiểu, docs/09-van-hanh.md mục 7).</summary>
    public const string AlertTag = "alert";

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

    /// <summary>
    /// Như <see cref="WriteAsync"/>, thêm mô tả và số liệu của từng check (không có exception).
    /// Chỉ dùng cho /health/alerts, endpoint bị Nginx chặn từ bên ngoài (deploy/nginx/snippets/app.conf).
    /// </summary>
    public static Task WriteDetailedAsync(HttpContext context, HealthReport report)
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
                description = e.Value.Exception is null ? e.Value.Description : "Check ném lỗi; xem log của API.",
                data = e.Value.Data,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}

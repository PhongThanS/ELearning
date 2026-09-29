using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using ELearning.Api.Common;
using ELearning.Infrastructure.Security;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.RateLimiting;

namespace ELearning.Api.Security;

/// <summary>Các policy rate limit (docs/07-bao-mat.md mục 5).</summary>
public static class RateLimitPolicies
{
    public const string AuthLogin = "auth-login";
    public const string AuthRefresh = "auth-refresh";
    public const string AttemptWrite = "attempt-write";
    public const string AttemptAction = "attempt-action";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimits");
        int Limit(string name, int fallback) => section.GetValue(name, fallback);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(AuthLogin, ctx => RateLimitPartition.GetFixedWindowLimiter(
                $"login:{IpKey(ctx)}",
                _ => FixedWindow(Limit("AuthLoginPerMinute", 600))));

            options.AddPolicy(AuthRefresh, ctx => RateLimitPartition.GetFixedWindowLimiter(
                $"refresh:{IpKey(ctx)}",
                _ => FixedWindow(Limit("AuthRefreshPerMinute", 1200))));

            options.AddPolicy(AttemptWrite, ctx => RateLimitPartition.GetTokenBucketLimiter(
                $"attempt-write:{UserKey(ctx)}",
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = Limit("AttemptWriteBurst", 20),
                    TokensPerPeriod = Limit("AttemptWritePerMinute", 60),
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));

            options.AddPolicy(AttemptAction, ctx => RateLimitPartition.GetFixedWindowLimiter(
                $"attempt-action:{UserKey(ctx)}",
                _ => FixedWindow(Limit("AttemptActionPerMinute", 10))));

            // Mặc định cho mọi endpoint còn lại
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"default:{UserKey(ctx)}",
                    _ => FixedWindow(Limit("DefaultPerMinute", 300))));
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions FixedWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private static string IpKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string UserKey(HttpContext ctx) =>
        ctx.User.FindFirst(AuthClaimTypes.Subject)?.Value is { } userId ? $"u:{userId}" : $"ip:{IpKey(ctx)}";

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var error = new Error(ErrorType.RateLimited, ErrorCodes.RateLimited, "Bạn thao tác quá nhanh. Vui lòng thử lại sau.");
        await response.WriteAsJsonAsync(
            ApiResponse.Fail(error, Activity.Current?.Id ?? context.HttpContext.TraceIdentifier), ct);
    }
}

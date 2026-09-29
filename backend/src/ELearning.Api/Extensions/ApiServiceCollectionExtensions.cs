using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ELearning.Api.Common;
using ELearning.Api.Monitoring;
using ELearning.Application.Common.Options;
using ELearning.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ELearning.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Options có validation; thiếu hoặc sai cấu hình thì ứng dụng không khởi động (docs/09-van-hanh.md mục 1).</summary>
    public static IServiceCollection AddValidatedOptions(this IServiceCollection services)
    {
        services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => TimeZoneExists(o.BusinessTimeZone), "App:BusinessTimeZone không phải múi giờ hợp lệ.")
            .ValidateOnStart();
        services.AddOptions<ExamOptions>().BindConfiguration(ExamOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MonitoringOptions>().BindConfiguration(MonitoringOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MediaOptions>().BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services) =>
        services.AddSerilog((sp, logger) => logger
            .ReadFrom.Configuration(sp.GetRequiredService<IConfiguration>())
            .ReadFrom.Services(sp)
            .Enrich.FromLogContext());

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers(o => o.ModelBinderProviders.Insert(0, new SnakeCaseEnumModelBinderProvider()))
            .AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = StatusCodeResponses.InvalidModelState);

        // Minimal API / WriteAsJsonAsync dùng cùng cấu hình JSON
        services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>JSON: camelCase, enum UPPER_SNAKE_CASE (docs/05-api.md mục 1).</summary>
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
    }

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddMetrics();
        services.AddHealthChecks()
            .AddDbContextCheck<ELearningDbContext>("database", tags: [HealthCheckResponseWriter.ReadyTag, HealthCheckResponseWriter.AlertTag])
            .AddCheck<ErrorRateHealthCheck>("error-rate", tags: [HealthCheckResponseWriter.AlertTag])
            .AddCheck<AttemptBacklogHealthCheck>("attempt-backlog", tags: [HealthCheckResponseWriter.AlertTag])
            .AddCheck<ExpirationWorkerHealthCheck>("expiration-worker", tags: [HealthCheckResponseWriter.AlertTag]);
        return services;
    }

    /// <summary>Chỉ tin X-Forwarded-* từ proxy đã khai báo (docs/07-bao-mat.md mục 5).</summary>
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            // Dải mạng của proxy (CIDR), ví dụ mạng Docker của Nginx có IP container thay đổi mỗi lần tạo lại
            foreach (var network in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        return services;
    }

    private static bool TimeZoneExists(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}

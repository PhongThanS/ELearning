using ELearning.Api.Common;
using ELearning.Api.Extensions;
using ELearning.Api.Security;
using ELearning.Application;
using ELearning.Application.Common.Abstractions;
using ELearning.Infrastructure;
using ELearning.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// Thứ tự khởi động theo docs/09-van-hanh.md mục 2.
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddValidatedOptions()
    .AddSerilogLogging()
    .AddInfrastructure()
    .AddApplication()
    .AddJwtAuthentication()
    .AddApiAuthorization()
    .AddApiControllers()
    .AddApiRateLimiting(builder.Configuration)
    .AddReverseProxySupport(builder.Configuration)
    .AddOpenApi()
    .AddApiHealthChecks();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<RefreshTokenCookie>();

var app = builder.Build();

// Seed: tự động ở Development; môi trường khác chạy "dotnet ELearning.Api.dll --seed" sau khi áp migration.
var seedOnly = args.Contains("--seed", StringComparer.Ordinal);
if (seedOnly || app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(app.Environment.IsDevelopment());
    if (seedOnly)
    {
        return;
    }
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeResponses.WriteAsync);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

if (!app.Environment.IsProduction())
{
    // Swagger UI là middleware, đặt trước xác thực để không bị FallbackPolicy chặn.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ELearning API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckResponseWriter.ReadyTag),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

await app.RunAsync();

/// <summary>Cho WebApplicationFactory trong ELearning.ApiTests.</summary>
public partial class Program;

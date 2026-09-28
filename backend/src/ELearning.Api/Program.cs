using ELearning.Api.Common;
using ELearning.Api.Extensions;
using ELearning.Application;
using ELearning.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// Thứ tự khởi động theo docs/09-van-hanh.md mục 2.
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddValidatedOptions()
    .AddSerilogLogging()
    .AddInfrastructure()
    .AddApplication()
    .AddApiControllers()
    .AddReverseProxySupport(builder.Configuration)
    .AddOpenApi()
    .AddApiHealthChecks();

var app = builder.Build();

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
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ELearning API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckResponseWriter.ReadyTag),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

await app.RunAsync();

/// <summary>Cho WebApplicationFactory trong ELearning.ApiTests.</summary>
public partial class Program;

using System.Net;
using System.Text.Json;
using ELearning.Api.Extensions;
using ELearning.Domain.Enums;

namespace ELearning.ApiTests.Infrastructure;

[Collection(ApiCollection.Name)]
public class PlatformEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Live_health_check_is_healthy()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Every_response_tells_how_long_the_server_took_so_slowness_can_be_split_from_the_network()
    {
        var anonymous = await _client.GetAsync(new Uri("/api/khong-ton-tai", UriKind.Relative));
        var health = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        anonymous.Headers.GetValues("Server-Timing").Should().ContainSingle().Which.Should().MatchRegex(@"^app;dur=\d+\.\d$");
        health.Headers.Contains("Server-Timing").Should().BeTrue();
    }

    [Theory]
    [InlineData(200, 20, null, Serilog.Events.LogEventLevel.Information)]
    [InlineData(200, 501, null, Serilog.Events.LogEventLevel.Warning)]
    [InlineData(404, 900, null, Serilog.Events.LogEventLevel.Warning)]
    [InlineData(500, 10, null, Serilog.Events.LogEventLevel.Error)]
    [InlineData(200, 10, "boom", Serilog.Events.LogEventLevel.Error)]
    public void Slow_requests_are_logged_as_warnings_and_failures_as_errors(int status, double ms, string? error, Serilog.Events.LogEventLevel expected)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Response.StatusCode = status;

        ELearning.Api.Monitoring.RequestLogLevel.Get(context, ms, error is null ? null : new InvalidOperationException(error))
            .Should().Be(expected);
    }

    [Fact]
    public async Task Ready_health_check_verifies_database()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        json.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("database");
    }

    [Fact]
    public async Task Anonymous_request_to_unknown_route_is_rejected_by_fallback_policy()
    {
        var response = await _client.GetAsync(new Uri("/api/khong-ton-tai", UriKind.Relative));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("UNAUTHENTICATED");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Authenticated_request_to_unknown_route_returns_not_found_envelope()
    {
        var client = factory.CreateHttpsClient();
        await client.LoginAsync(ApiFactory.StudentUserName, ApiFactory.StudentPassword);

        var (response, body) = await client.GetJsonAsync<object>("/api/khong-ton-tai");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Errors.Single().Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task OpenApi_document_is_available_outside_production()
    {
        var response = await _client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Api_json_serializes_enums_as_upper_snake_case_and_dates_with_z()
    {
        var options = new JsonSerializerOptions();
        ApiServiceCollectionExtensions.ConfigureJson(options);

        var json = JsonSerializer.Serialize(
            new { Status = AttemptStatus.AutoSubmitted, At = new DateTime(2026, 9, 26, 3, 0, 0, DateTimeKind.Utc) },
            options);

        json.Should().Be("""{"status":"AUTO_SUBMITTED","at":"2026-09-26T03:00:00Z"}""");
    }
}

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
    public async Task Ready_health_check_verifies_database()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        json.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("database");
    }

    [Fact]
    public async Task Unknown_route_returns_api_response_envelope()
    {
        var response = await _client.GetAsync(new Uri("/api/khong-ton-tai", UriKind.Relative));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("NOT_FOUND");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
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

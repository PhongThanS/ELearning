using System.Net;
using System.Text.Json;
using ELearning.ApiTests.Attempts;
using ELearning.Application.Attempts;
using ELearning.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.ApiTests.Infrastructure;

/// <summary>Database và đồng hồ riêng: lượt quá hạn và lỗi 5xx giả lập ở đây không lẫn sang test khác.</summary>
public sealed class MonitoringApiFactory : ApiFactory;

/// <summary>Cảnh báo tối thiểu qua /health/alerts (docs/09-van-hanh.md mục 7).</summary>
public class AlertEndpointTests(MonitoringApiFactory factory) : IClassFixture<MonitoringApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Alerts_endpoint_lists_every_alert_check_and_ready_stays_database_only()
    {
        var (status, alerts) = await GetAlertsAsync();
        var ready = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());

        status.Should().Be(HttpStatusCode.OK);
        alerts.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        Checks(alerts).Keys.Should().BeEquivalentTo("database", "error-rate", "attempt-backlog", "expiration-worker");
        Checks(alerts)["expiration-worker"].GetProperty("description").GetString().Should()
            .Contain("không chạy trên instance này", "ApiFactory tắt job tự nộp");
        readyJson.RootElement.GetProperty("checks").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Overdue_attempt_raises_backlog_alert_until_it_is_auto_submitted()
    {
        var kit = await AttemptTestKit.CreateAsync(factory);
        var exam = await kit.CreatePublishedExamAsync(durationMinutes: 1);
        var (student, _, _) = await kit.CreateStudentAsync();
        await AttemptTestKit.StartAsync(student, exam.Id);

        factory.Time.Advance(TimeSpan.FromMinutes(1 + 5) + TimeSpan.FromSeconds(1));
        var (stale, staleJson) = await GetAlertsAsync();
        var backlog = Checks(staleJson)["attempt-backlog"];

        stale.Should().Be(HttpStatusCode.ServiceUnavailable);
        backlog.GetProperty("status").GetString().Should().Be("Unhealthy");
        backlog.GetProperty("data").GetProperty("overdue").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        backlog.GetProperty("data").GetProperty("oldestOverdueSeconds").GetDouble().Should().BeGreaterThan(5 * 60);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAttemptExpirationService>().ProcessExpiredAsync(CancellationToken.None);
        }

        var (_, sweptJson) = await GetAlertsAsync();
        Checks(sweptJson)["attempt-backlog"].GetProperty("status").GetString().Should().Be("Healthy");
        Checks(sweptJson)["attempt-backlog"].GetProperty("data").GetProperty("overdue").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Server_error_rate_above_threshold_raises_alert_and_clears_after_the_window()
    {
        var metrics = factory.Services.GetRequiredService<OperationalMetrics>();
        for (var i = 0; i < 60; i++)
        {
            metrics.RecordRequest(i < 3 ? 500 : 200);
        }

        var (status, json) = await GetAlertsAsync();
        var errorRate = Checks(json)["error-rate"];

        status.Should().Be(HttpStatusCode.ServiceUnavailable);
        errorRate.GetProperty("status").GetString().Should().Be("Unhealthy");
        errorRate.GetProperty("data").GetProperty("serverErrors").GetInt64().Should().Be(3);

        factory.Time.Advance(TimeSpan.FromMinutes(6));
        var (_, cleared) = await GetAlertsAsync();
        Checks(cleared)["error-rate"].GetProperty("status").GetString().Should().Be("Healthy", "lỗi đã ra khỏi cửa sổ 5 phút");
    }

    private async Task<(HttpStatusCode Status, JsonDocument Json)> GetAlertsAsync()
    {
        var response = await _client.GetAsync(new Uri("/health/alerts", UriKind.Relative));
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()));
    }

    private static Dictionary<string, JsonElement> Checks(JsonDocument json) =>
        json.RootElement.GetProperty("checks").EnumerateArray().ToDictionary(c => c.GetProperty("name").GetString()!);
}

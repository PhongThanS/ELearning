using System.Diagnostics.Metrics;
using ELearning.Application.Common.Options;
using ELearning.Application.Monitoring;
using Microsoft.Extensions.Time.Testing;

namespace ELearning.UnitTests.Monitoring;

/// <summary>Chỉ số và điều kiện cảnh báo tối thiểu (docs/09-van-hanh.md mục 7).</summary>
public class MonitoringTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly MonitoringOptions Thresholds = new();
    private static readonly ExamOptions Exam = new();

    [Fact]
    public void Rolling_counter_forgets_events_older_than_the_window()
    {
        var time = new FakeTimeProvider(Start);
        var counter = new RollingCounter(TimeSpan.FromMinutes(5), time);

        counter.Add(3);
        time.Advance(TimeSpan.FromMinutes(2));
        counter.Add();
        counter.Total().Should().Be(4);

        time.Advance(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(10));
        counter.Total().Should().Be(1, "3 sự kiện đầu đã ra khỏi cửa sổ 5 phút");

        time.Advance(TimeSpan.FromMinutes(10));
        counter.Add(2);
        counter.Total().Should().Be(2, "ô cũ được ghi đè khi quay vòng");
    }

    [Fact]
    public void Error_rate_alert_needs_enough_requests_and_rate_above_threshold()
    {
        AlertRules.ErrorRateExceeded(Window(requests: 49, serverErrors: 49), Thresholds).Should().BeFalse("chưa đủ 50 request");
        AlertRules.ErrorRateExceeded(Window(requests: 100, serverErrors: 1), Thresholds).Should().BeFalse("đúng 1% chưa vượt ngưỡng");
        AlertRules.ErrorRateExceeded(Window(requests: 100, serverErrors: 2), Thresholds).Should().BeTrue();
        AlertRules.ErrorRatePercent(Window(requests: 0, serverErrors: 0)).Should().Be(0);
    }

    [Fact]
    public void Backlog_alert_when_an_overdue_attempt_waits_longer_than_threshold()
    {
        var now = Start.UtcDateTime;

        AlertRules.OverdueAttemptsStale(new AttemptBacklog(10, 0, null, now), Thresholds).Should().BeFalse();
        AlertRules.OverdueAttemptsStale(new AttemptBacklog(10, 3, now.AddMinutes(-5), now), Thresholds).Should().BeFalse("đúng 5 phút");
        AlertRules.OverdueAttemptsStale(new AttemptBacklog(10, 3, now.AddMinutes(-5).AddSeconds(-1), now), Thresholds).Should().BeTrue();
    }

    [Fact]
    public void Worker_stalls_after_three_missed_sweeps_and_is_ignored_when_disabled()
    {
        var now = Start.UtcDateTime;

        AlertRules.WorkerStalled(null, null, now, Exam).Should().BeFalse("job tắt trên instance này");
        AlertRules.WorkerStalled(now.AddSeconds(-180), null, now, Exam).Should().BeFalse("vừa đủ 3 chu kỳ 60 giây");
        AlertRules.WorkerStalled(now.AddSeconds(-181), null, now, Exam).Should().BeTrue("chưa quét xong lần nào");
        AlertRules.WorkerStalled(now.AddHours(-1), now.AddSeconds(-30), now, Exam).Should().BeFalse();
        AlertRules.WorkerStalled(now.AddHours(-1), now.AddSeconds(-200), now, Exam).Should().BeTrue();
    }

    [Fact]
    public void Metrics_publish_to_meter_and_keep_window_totals()
    {
        var time = new FakeTimeProvider(Start);
        using var meterFactory = new TestMeterFactory();
        var metrics = new OperationalMetrics(meterFactory, time, Microsoft.Extensions.Options.Options.Create(Thresholds));
        var published = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == OperationalMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            published[instrument.Name] = published.GetValueOrDefault(instrument.Name) + value);
        listener.Start();

        metrics.RecordRequest(200);
        metrics.RecordRequest(503);
        metrics.RecordLoginFailure();
        metrics.RecordGradingFailure();
        metrics.RecordDatabaseError();
        metrics.RecordSweep(processed: 7, failed: 1, TimeSpan.FromSeconds(2));

        metrics.Window().Should().BeEquivalentTo(new MetricsWindow(TimeSpan.FromMinutes(5), 2, 1, 1, 1, 1, 7, 1));
        metrics.LastSweepAt.Should().Be(Start.UtcDateTime);
        published["elearning.http.server_errors"].Should().Be(1);
        published["elearning.auth.login_failures"].Should().Be(1);
        published["elearning.attempts.auto_submitted"].Should().Be(7);
    }

    [Fact]
    public void Oldest_overdue_seconds_is_measured_from_expired_at()
    {
        var now = Start.UtcDateTime;

        new AttemptBacklog(5, 2, now.AddSeconds(-90), now).OldestOverdueSeconds.Should().Be(90);
        new AttemptBacklog(5, 0, null, now).OldestOverdueSeconds.Should().Be(0);
    }

    private static MetricsWindow Window(long requests, long serverErrors) =>
        new(TimeSpan.FromMinutes(5), requests, serverErrors, 0, 0, 0, 0, 0);

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose() => _meters.ForEach(m => m.Dispose());
    }
}

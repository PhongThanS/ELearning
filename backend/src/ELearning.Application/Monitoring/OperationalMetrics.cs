using System.Diagnostics.Metrics;
using ELearning.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Monitoring;

/// <summary>Số lượt đang làm / quá hạn đo ở một thời điểm (docs/09-van-hanh.md mục 7).</summary>
public sealed record AttemptBacklog(int InProgress, int Overdue, DateTime? OldestOverdueExpiredAt, DateTime MeasuredAt)
{
    /// <summary>Lượt quá hạn lâu nhất đã quá hạn bao lâu (0 nếu không có) — "độ trễ worker".</summary>
    public double OldestOverdueSeconds =>
        OldestOverdueExpiredAt is { } expiredAt ? Math.Max(0, (MeasuredAt - expiredAt).TotalSeconds) : 0;
}

/// <summary>Tổng số trong cửa sổ MonitoringOptions.ErrorRateWindowMinutes.</summary>
public sealed record MetricsWindow(
    TimeSpan Length,
    long Requests,
    long ServerErrors,
    long LoginFailures,
    long GradingFailures,
    long DatabaseErrors,
    long AutoSubmitted,
    long SweepFailures);

/// <summary>
/// Chỉ số vận hành. Ghi ra <see cref="Meter"/> "ELearning" (đọc bằng dotnet-counters, sau này OpenTelemetry)
/// và giữ tổng trong cửa sổ trượt để health check /health/alerts và log "Monitoring" dùng (D-26).
/// Thời gian xử lý request p50 / p95 theo endpoint lấy từ meter có sẵn <c>Microsoft.AspNetCore.Hosting</c>.
/// </summary>
public sealed class OperationalMetrics
{
    public const string MeterName = "ELearning";

    private readonly TimeProvider _time;
    private readonly TimeSpan _window;

    private readonly Counter<long> _serverErrors;
    private readonly Counter<long> _loginFailures;
    private readonly Counter<long> _gradingFailures;
    private readonly Counter<long> _databaseErrors;
    private readonly Counter<long> _autoSubmitted;
    private readonly Counter<long> _sweepFailures;
    private readonly Histogram<double> _sweepDuration;

    private readonly RollingCounter _recentRequests;
    private readonly RollingCounter _recentServerErrors;
    private readonly RollingCounter _recentLoginFailures;
    private readonly RollingCounter _recentGradingFailures;
    private readonly RollingCounter _recentDatabaseErrors;
    private readonly RollingCounter _recentAutoSubmitted;
    private readonly RollingCounter _recentSweepFailures;

    private long _workerStartedTicks;
    private long _lastSweepTicks;
    private AttemptBacklog? _backlog;

    public OperationalMetrics(IMeterFactory meterFactory, TimeProvider time, IOptions<MonitoringOptions> options)
    {
        _time = time;
        _window = TimeSpan.FromMinutes(options.Value.ErrorRateWindowMinutes);

        var meter = meterFactory.Create(MeterName);
        _serverErrors = meter.CreateCounter<long>("elearning.http.server_errors", "{response}", "Response 5xx, không tính /health");
        _loginFailures = meter.CreateCounter<long>("elearning.auth.login_failures", "{attempt}", "Đăng nhập sai hoặc bị khóa");
        _gradingFailures = meter.CreateCounter<long>("elearning.grading.failures", "{attempt}", "Chấm điểm ném lỗi");
        _databaseErrors = meter.CreateCounter<long>("elearning.db.errors", "{error}", "Lỗi SQL Server không phải trùng khóa / xung đột");
        _autoSubmitted = meter.CreateCounter<long>("elearning.attempts.auto_submitted", "{attempt}", "Lượt được job tự nộp");
        _sweepFailures = meter.CreateCounter<long>("elearning.attempts.auto_submit_failures", "{attempt}", "Lượt job tự nộp thất bại");
        _sweepDuration = meter.CreateHistogram<double>("elearning.attempts.sweep.duration", "s", "Thời gian một vòng quét tự nộp");
        meter.CreateObservableGauge("elearning.attempts.in_progress", () => _backlog?.InProgress ?? 0, "{attempt}", "Lượt đang làm");
        meter.CreateObservableGauge("elearning.attempts.overdue", () => _backlog?.Overdue ?? 0, "{attempt}", "Lượt quá hạn chưa nộp");
        meter.CreateObservableGauge(
            "elearning.attempts.oldest_overdue", () => _backlog?.OldestOverdueSeconds ?? 0, "s", "Lượt quá hạn lâu nhất chưa nộp");

        _recentRequests = new RollingCounter(_window, time);
        _recentServerErrors = new RollingCounter(_window, time);
        _recentLoginFailures = new RollingCounter(_window, time);
        _recentGradingFailures = new RollingCounter(_window, time);
        _recentDatabaseErrors = new RollingCounter(_window, time);
        _recentAutoSubmitted = new RollingCounter(_window, time);
        _recentSweepFailures = new RollingCounter(_window, time);
    }

    /// <summary>Thời điểm AttemptExpirationWorker bắt đầu chạy trên instance này; null nếu job tắt.</summary>
    public DateTime? WorkerStartedAt => FromTicks(Interlocked.Read(ref _workerStartedTicks));

    /// <summary>Thời điểm vòng quét tự nộp gần nhất chạy xong.</summary>
    public DateTime? LastSweepAt => FromTicks(Interlocked.Read(ref _lastSweepTicks));

    public AttemptBacklog? Backlog => Volatile.Read(ref _backlog);

    public void RecordRequest(int statusCode)
    {
        _recentRequests.Add();
        if (statusCode >= 500)
        {
            _recentServerErrors.Add();
            _serverErrors.Add(1);
        }
    }

    public void RecordLoginFailure()
    {
        _recentLoginFailures.Add();
        _loginFailures.Add(1);
    }

    public void RecordGradingFailure()
    {
        _recentGradingFailures.Add();
        _gradingFailures.Add(1);
    }

    public void RecordDatabaseError()
    {
        _recentDatabaseErrors.Add();
        _databaseErrors.Add(1);
    }

    public void RecordWorkerStarted() => Interlocked.Exchange(ref _workerStartedTicks, Now.Ticks);

    public void RecordSweep(int processed, int failed, TimeSpan duration)
    {
        _recentAutoSubmitted.Add(processed);
        _autoSubmitted.Add(processed);
        _recentSweepFailures.Add(failed);
        _sweepFailures.Add(failed);
        _sweepDuration.Record(duration.TotalSeconds);
        Interlocked.Exchange(ref _lastSweepTicks, Now.Ticks);
    }

    public void UpdateBacklog(AttemptBacklog backlog) => Volatile.Write(ref _backlog, backlog);

    public MetricsWindow Window() => new(
        _window,
        _recentRequests.Total(),
        _recentServerErrors.Total(),
        _recentLoginFailures.Total(),
        _recentGradingFailures.Total(),
        _recentDatabaseErrors.Total(),
        _recentAutoSubmitted.Total(),
        _recentSweepFailures.Total());

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private static DateTime? FromTicks(long ticks) => ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
}

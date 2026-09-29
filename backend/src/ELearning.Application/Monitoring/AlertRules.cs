using ELearning.Application.Common.Options;

namespace ELearning.Application.Monitoring;

/// <summary>
/// Điều kiện cảnh báo tối thiểu (docs/09-van-hanh.md mục 7). Dùng chung cho health check /health/alerts
/// và dòng log "Monitoring", để hai nơi luôn báo giống nhau.
/// </summary>
public static class AlertRules
{
    /// <summary>Số vòng quét liên tiếp bị lỡ thì coi là job tự nộp đã ngừng.</summary>
    public const int MissedSweepsBeforeStalled = 3;

    public static double ErrorRatePercent(MetricsWindow window) =>
        window.Requests == 0 ? 0 : window.ServerErrors * 100.0 / window.Requests;

    /// <summary>Tỉ lệ 5xx vượt ngưỡng, khi đủ số request để tỉ lệ có ý nghĩa.</summary>
    public static bool ErrorRateExceeded(MetricsWindow window, MonitoringOptions options) =>
        window.Requests >= options.ErrorRateMinRequests && ErrorRatePercent(window) > options.ErrorRatePercent;

    /// <summary>Có lượt quá hạn lâu hơn OverdueAttemptMinutes mà chưa được nộp.</summary>
    public static bool OverdueAttemptsStale(AttemptBacklog backlog, MonitoringOptions options) =>
        backlog.OldestOverdueSeconds > options.OverdueAttemptMinutes * 60;

    /// <summary>
    /// Job tự nộp đang chạy trên instance này nhưng đã lỡ <see cref="MissedSweepsBeforeStalled"/> vòng quét.
    /// Job tắt (<paramref name="workerStartedAt"/> null) thì không cảnh báo.
    /// </summary>
    public static bool WorkerStalled(DateTime? workerStartedAt, DateTime? lastSweepAt, DateTime now, ExamOptions exam) =>
        workerStartedAt is { } started
        && now - (lastSweepAt ?? started) > TimeSpan.FromSeconds(exam.ExpirationSweepIntervalSeconds * MissedSweepsBeforeStalled);
}

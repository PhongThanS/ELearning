using ELearning.Application.Common.Options;
using ELearning.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.BackgroundJobs;

/// <summary>
/// Mỗi Monitoring:SnapshotIntervalSeconds ghi một dòng log "Monitoring" có cấu trúc với các chỉ số ở
/// docs/09-van-hanh.md mục 7, để công cụ gom log (Seq / Elastic) vẽ biểu đồ và đặt cảnh báo mà không cần hệ thống metric riêng.
/// Điều kiện cảnh báo được ghi thêm ở mức Warning, cùng quy tắc với /health/alerts (<see cref="AlertRules"/>).
/// </summary>
internal sealed class MonitoringSnapshotWorker(
    IServiceScopeFactory scopeFactory,
    OperationalMetrics metrics,
    TimeProvider time,
    IOptions<MonitoringOptions> monitoringOptions,
    IOptions<ExamOptions> examOptions,
    ILogger<MonitoringSnapshotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = monitoringOptions.Value;
        if (!options.SnapshotEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SnapshotIntervalSeconds), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await WriteSnapshotAsync(options, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Không đo được chỉ số giám sát");
            }
        }
    }

    private async Task WriteSnapshotAsync(MonitoringOptions options, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var backlog = await scope.ServiceProvider.GetRequiredService<IAttemptBacklogQuery>().MeasureAsync(ct);
        var window = metrics.Window();

        logger.LogInformation(
            "Monitoring: {Requests} request, {ServerErrors} lỗi 5xx ({ErrorRatePercent:0.##}%) trong {WindowMinutes} phút; "
            + "{LoginFailures} đăng nhập thất bại, {GradingFailures} lỗi chấm, {DatabaseErrors} lỗi DB, "
            + "{AutoSubmitted} lượt tự nộp ({SweepFailures} lỗi); {InProgress} lượt đang làm, {Overdue} quá hạn, "
            + "trễ nhất {OldestOverdueSeconds:0} giây; quét lần cuối {LastSweepAt}",
            window.Requests,
            window.ServerErrors,
            AlertRules.ErrorRatePercent(window),
            window.Length.TotalMinutes,
            window.LoginFailures,
            window.GradingFailures,
            window.DatabaseErrors,
            window.AutoSubmitted,
            window.SweepFailures,
            backlog.InProgress,
            backlog.Overdue,
            backlog.OldestOverdueSeconds,
            metrics.LastSweepAt);

        if (AlertRules.ErrorRateExceeded(window, options))
        {
            logger.LogWarning(
                "ALERT error-rate: {ErrorRatePercent:0.##}% lỗi 5xx trong {WindowMinutes} phút (ngưỡng {Threshold}%)",
                AlertRules.ErrorRatePercent(window),
                window.Length.TotalMinutes,
                options.ErrorRatePercent);
        }

        if (AlertRules.OverdueAttemptsStale(backlog, options))
        {
            logger.LogWarning(
                "ALERT attempt-backlog: {Overdue} lượt quá hạn chưa nộp, lâu nhất {OldestOverdueSeconds:0} giây (ngưỡng {Threshold} phút)",
                backlog.Overdue,
                backlog.OldestOverdueSeconds,
                options.OverdueAttemptMinutes);
        }

        if (AlertRules.WorkerStalled(metrics.WorkerStartedAt, metrics.LastSweepAt, time.GetUtcNow().UtcDateTime, examOptions.Value))
        {
            logger.LogWarning("ALERT expiration-worker: job tự nộp không chạy xong vòng quét nào từ {LastSweepAt}", metrics.LastSweepAt);
        }
    }
}

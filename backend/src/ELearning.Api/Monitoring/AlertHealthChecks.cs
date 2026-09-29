using ELearning.Application.Common.Options;
using ELearning.Application.Monitoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ELearning.Api.Monitoring;

/// <summary>Tỉ lệ 5xx trong cửa sổ trượt vượt ngưỡng (mặc định 1% trong 5 phút).</summary>
internal sealed class ErrorRateHealthCheck(OperationalMetrics metrics, IOptions<MonitoringOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var window = metrics.Window();
        var data = new Dictionary<string, object>
        {
            ["windowMinutes"] = o.ErrorRateWindowMinutes,
            ["requests"] = window.Requests,
            ["serverErrors"] = window.ServerErrors,
            ["errorRatePercent"] = Math.Round(AlertRules.ErrorRatePercent(window), 2),
            ["thresholdPercent"] = o.ErrorRatePercent,
        };

        return Task.FromResult(AlertRules.ErrorRateExceeded(window, o)
            ? HealthCheckResult.Unhealthy($"Tỉ lệ lỗi 5xx vượt {o.ErrorRatePercent}% trong {o.ErrorRateWindowMinutes} phút.", data: data)
            : HealthCheckResult.Healthy(data: data));
    }
}

/// <summary>Có lượt quá hạn lâu hơn OverdueAttemptMinutes mà chưa được nộp (job tự nộp chậm hoặc lỗi).</summary>
internal sealed class AttemptBacklogHealthCheck(IAttemptBacklogQuery backlogQuery, IOptions<MonitoringOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var backlog = await backlogQuery.MeasureAsync(cancellationToken);
        var data = new Dictionary<string, object>
        {
            ["inProgress"] = backlog.InProgress,
            ["overdue"] = backlog.Overdue,
            ["oldestOverdueSeconds"] = Math.Round(backlog.OldestOverdueSeconds),
            ["thresholdMinutes"] = o.OverdueAttemptMinutes,
        };

        return AlertRules.OverdueAttemptsStale(backlog, o)
            ? HealthCheckResult.Unhealthy($"Có lượt thi quá hạn hơn {o.OverdueAttemptMinutes} phút mà chưa được nộp.", data: data)
            : HealthCheckResult.Healthy(data: data);
    }
}

/// <summary>Job tự nộp trên instance này đã lỡ nhiều vòng quét liên tiếp.</summary>
internal sealed class ExpirationWorkerHealthCheck(OperationalMetrics metrics, TimeProvider time, IOptions<ExamOptions> examOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (metrics.WorkerStartedAt is null)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Job tự nộp không chạy trên instance này (Exam:ExpirationWorkerEnabled)."));
        }

        var data = new Dictionary<string, object>
        {
            ["intervalSeconds"] = examOptions.Value.ExpirationSweepIntervalSeconds,
        };
        if (metrics.LastSweepAt is { } lastSweepAt)
        {
            data["lastSweepAt"] = lastSweepAt;
        }

        return Task.FromResult(AlertRules.WorkerStalled(metrics.WorkerStartedAt, metrics.LastSweepAt, time.GetUtcNow().UtcDateTime, examOptions.Value)
            ? HealthCheckResult.Unhealthy(
                $"Job tự nộp không hoàn thành vòng quét nào trong {AlertRules.MissedSweepsBeforeStalled} chu kỳ.", data: data)
            : HealthCheckResult.Healthy(data: data));
    }
}

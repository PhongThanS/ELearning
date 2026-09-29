using ELearning.Application.Attempts;
using ELearning.Application.Common.Options;
using ELearning.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.BackgroundJobs;

/// <summary>
/// Tự nộp các lượt quá hạn mà học viên đã bỏ đi (D-05). Chạy mỗi Exam:ExpirationSweepIntervalSeconds.
/// An toàn khi nhiều instance cùng chạy vì mỗi lượt được khóa dòng và kiểm tra trạng thái trước khi nộp.
/// </summary>
internal sealed class AttemptExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<ExamOptions> options,
    OperationalMetrics metrics,
    ILogger<AttemptExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ExpirationWorkerEnabled)
        {
            logger.LogInformation("AttemptExpirationWorker đang tắt (Exam:ExpirationWorkerEnabled = false).");
            return;
        }

        // Mốc để /health/alerts phát hiện job ngừng quét (docs/09-van-hanh.md mục 7)
        metrics.RecordWorkerStarted();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ExpirationSweepIntervalSeconds), time);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processed = await scope.ServiceProvider.GetRequiredService<IAttemptExpirationService>()
                    .ProcessExpiredAsync(stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Đã tự nộp {Count} lượt thi quá hạn", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Vòng quét lượt thi quá hạn thất bại");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

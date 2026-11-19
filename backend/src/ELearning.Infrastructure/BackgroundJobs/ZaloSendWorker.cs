using ELearning.Application.Zalo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.BackgroundJobs;

/// <summary>Mỗi Zalo:PollSeconds lấy các tin Zalo đang chờ trong hàng đợi và gửi đi (D-45).</summary>
internal sealed class ZaloSendWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<ZaloOptions> options,
    ILogger<ZaloSendWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsEnabled || !settings.WorkerEnabled)
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds), time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var sent = await scope.ServiceProvider.GetRequiredService<IZaloDispatchService>().DispatchPendingAsync(stoppingToken);
                    if (sent > 0)
                    {
                        logger.LogInformation("Đã xử lý {Count} tin Zalo", sent);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Không gửi được lượt tin Zalo");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Dừng ứng dụng
        }
    }
}

using ELearning.Application.Common.Options;
using ELearning.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.BackgroundJobs;

/// <summary>
/// Mỗi UserRetention:SweepIntervalMinutes xóa cứng các tài khoản đã khóa quá UserRetention:DeleteLockedAfterDays ngày (D-41).
/// Chạy lần đầu sau khi khởi động 2 phút.
/// </summary>
internal sealed class LockedUserCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<UserRetentionOptions> options,
    ILogger<LockedUserCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.DeleteLockedAfterDays <= 0)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), time, stoppingToken);
            await SweepAsync(stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.SweepIntervalMinutes), time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Dừng ứng dụng
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ILockedUserCleanupService>().DeleteExpiredAsync(ct);
            if (result.Deleted > 0 || result.Kept > 0)
            {
                logger.LogInformation(
                    "Dọn tài khoản khóa lâu: xóa {Deleted}, giữ lại {Kept} (tác giả nội dung)", result.Deleted, result.Kept);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Không dọn được tài khoản khóa lâu");
        }
    }
}

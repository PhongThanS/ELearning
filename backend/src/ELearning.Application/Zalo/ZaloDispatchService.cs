using System.Text.Json;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Zalo;

/// <summary>
/// Gửi các tin Pending trong hàng đợi (D-45). Mỗi tin chỉ thử một lần: kết quả mơ hồ (hết thời gian chờ, máy chủ dừng
/// giữa chừng) được ghi Failed để người dùng quyết định gửi lại, tránh phụ huynh nhận trùng tin.
/// </summary>
internal sealed class ZaloDispatchService(
    IAppDbContext db,
    IZaloSender sender,
    IOptions<ZaloOptions> options,
    TimeProvider time,
    ILogger<ZaloDispatchService> logger) : IZaloDispatchService
{
    /// <summary>Tin ở trạng thái Sending quá lâu (job chết giữa chừng) thì coi là lỗi không rõ kết quả.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(5);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsEnabled)
        {
            return 0;
        }

        await RecoverStuckAsync(ct);

        var ids = await db.ZaloMessages.AsNoTracking()
            .Where(m => m.Status == ZaloMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt).Select(m => m.Id)
            .Take(settings.BatchSize)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            // Nhận tin: chỉ một tiến trình nhận được (UPDATE có điều kiện); hủy / gửi lại cùng lúc thì bị bỏ qua
            var now = Now;
            var claimed = await db.ZaloMessages.Where(m => m.Id == id && m.Status == ZaloMessageStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, ZaloMessageStatus.Sending)
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                    .SetProperty(m => m.SendingAt, now), ct);
            if (claimed == 0)
            {
                continue;
            }

            db.ChangeTracker.Clear();
            var message = await db.ZaloMessages.SingleAsync(m => m.Id == id, ct);
            await SendOneAsync(message, ct);
            processed++;

            if (settings.SendIntervalMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(settings.SendIntervalMs), ct);
            }
        }

        return processed;
    }

    private async Task SendOneAsync(Domain.Zalo.ZaloMessage message, CancellationToken ct)
    {
        ZaloSendResult result;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message.TemplateData) ?? [];
            result = await sender.SendAsync(new ZaloSendRequest(message.Phone, message.TemplateId, data, message.TrackingId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Dừng ứng dụng giữa lúc gửi: kết quả không rõ, để RecoverStuckAsync xử lý lần sau
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi gửi tin Zalo {MessageId}", message.Id);
            result = ZaloSendResult.Fail("SEND_ERROR", "Không rõ kết quả gửi (lỗi mạng hoặc hết thời gian chờ). Kiểm tra Zalo của phụ huynh rồi gửi lại nếu cần.");
        }

        if (result.Success)
        {
            message.MarkSent(result.MessageId, Now);
        }
        else
        {
            message.MarkFailed(result.ErrorCode ?? "UNKNOWN", result.ErrorMessage);
            logger.LogWarning("Gửi tin Zalo {MessageId} lỗi: {Code}", message.Id, result.ErrorCode);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RecoverStuckAsync(CancellationToken ct)
    {
        var limit = Now - StuckAfter;
        var recovered = await db.ZaloMessages.Where(m => m.Status == ZaloMessageStatus.Sending && m.SendingAt < limit)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, ZaloMessageStatus.Failed)
                .SetProperty(m => m.ErrorCode, "INTERRUPTED")
                .SetProperty(m => m.ErrorMessage, "Không rõ kết quả gửi (máy chủ dừng giữa chừng). Kiểm tra Zalo của phụ huynh rồi gửi lại nếu cần."), ct);
        if (recovered > 0)
        {
            logger.LogWarning("Có {Count} tin Zalo kẹt ở trạng thái đang gửi, đã chuyển sang lỗi", recovered);
        }
    }
}

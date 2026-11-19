using ELearning.Application.Zalo;
using Microsoft.Extensions.Logging;

namespace ELearning.Infrastructure.Zalo;

/// <summary>Provider <c>Log</c>: không gửi gì ra ngoài, chỉ ghi log (thử luồng trên máy dev). Không ghi số điện thoại đầy đủ.</summary>
internal sealed class LogZaloSender(ILogger<LogZaloSender> logger) : IZaloSender
{
    public Task<ZaloSendResult> SendAsync(ZaloSendRequest request, CancellationToken ct)
    {
        var masked = request.Phone.Length > 4 ? $"{request.Phone[..4]}***{request.Phone[^2..]}" : "***";
        logger.LogInformation("[Zalo:Log] Giả lập gửi tới {Phone}, mẫu {TemplateId}, {Count} tham số", masked, request.TemplateId, request.Data.Count);
        return Task.FromResult(ZaloSendResult.Ok($"log-{request.TrackingId}"));
    }
}

/// <summary>Provider <c>Disabled</c>: mọi lần gửi đều lỗi (service đã chặn tạo đợt, đây là lớp phòng thủ cuối).</summary>
internal sealed class DisabledZaloSender : IZaloSender
{
    public Task<ZaloSendResult> SendAsync(ZaloSendRequest request, CancellationToken ct) =>
        Task.FromResult(ZaloSendResult.Fail("ZALO_DISABLED", "Gửi tin Zalo đang tắt."));
}

using System.Collections.Concurrent;
using ELearning.Application.Zalo;

namespace ELearning.ApiTests.Classes;

/// <summary>Sender giả cho test: ghi lại các tin đã "gửi"; số điện thoại trong <see cref="FailingPhones"/> trả lỗi.</summary>
public sealed class FakeZaloSender : IZaloSender
{
    public ConcurrentQueue<ZaloSendRequest> Sent { get; } = new();

    public ConcurrentDictionary<string, bool> FailingPhones { get; } = new();

    public Task<ZaloSendResult> SendAsync(ZaloSendRequest request, CancellationToken ct)
    {
        if (FailingPhones.ContainsKey(request.Phone))
        {
            return Task.FromResult(ZaloSendResult.Fail("ZNS_118", "Số điện thoại chưa đăng ký Zalo"));
        }

        Sent.Enqueue(request);
        return Task.FromResult(ZaloSendResult.Ok($"fake-{request.TrackingId}"));
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ELearning.Application.Zalo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Zalo;

/// <summary>
/// Gửi tin theo số điện thoại bằng Zalo Notification Service (ZNS) của Official Account: POST /message/template.
/// Lỗi token hết hạn thì đổi token và thử lại đúng một lần; các lỗi khác trả về để người dùng quyết định gửi lại.
/// </summary>
internal sealed class ZnsZaloSender(
    IHttpClientFactory httpFactory,
    ZaloTokenProvider tokens,
    IOptions<ZaloOptions> options,
    ILogger<ZnsZaloSender> logger) : IZaloSender
{
    private const string SendUrl = "https://business.openapi.zalo.me/message/template";

    /// <summary>Mã lỗi Zalo cho "access token không hợp lệ / hết hạn".</summary>
    private const int ErrorInvalidToken = -124;

    private sealed record Envelope(
        [property: JsonPropertyName("error")] int Error,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("data")] Payload? Data);

    private sealed record Payload([property: JsonPropertyName("msg_id")] string? MsgId);

    public async Task<ZaloSendResult> SendAsync(ZaloSendRequest request, CancellationToken ct)
    {
        try
        {
            var result = await PostAsync(request, forceRefresh: false, ct);
            if (result.Error == ErrorInvalidToken)
            {
                result = await PostAsync(request, forceRefresh: true, ct);
            }

            return result.Error == 0
                ? ZaloSendResult.Ok(result.Data?.MsgId)
                : ZaloSendResult.Fail($"ZNS_{Math.Abs(result.Error)}", result.Message);
        }
        catch (ZaloAuthException ex)
        {
            return ZaloSendResult.Fail("ZALO_AUTH", ex.Message);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException) && !ct.IsCancellationRequested)
        {
            // Mạng / hết thời gian chờ: Zalo có thể đã nhận tin → không tự thử lại để tránh gửi trùng
            logger.LogWarning(ex, "Gọi ZNS không có kết quả rõ ràng");
            return ZaloSendResult.Fail("SEND_ERROR", "Không rõ kết quả gửi (lỗi mạng hoặc hết thời gian chờ). Kiểm tra Zalo của phụ huynh rồi gửi lại nếu cần.");
        }
    }

    private async Task<Envelope> PostAsync(ZaloSendRequest request, bool forceRefresh, CancellationToken ct)
    {
        var token = await tokens.GetAccessTokenAsync(forceRefresh, ct);
        var body = new Dictionary<string, object?>
        {
            ["phone"] = request.Phone,
            ["template_id"] = request.TemplateId,
            ["template_data"] = request.Data,
            ["tracking_id"] = request.TrackingId,
        };
        if (!string.IsNullOrWhiteSpace(options.Value.Mode))
        {
            body["mode"] = options.Value.Mode;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, SendUrl) { Content = JsonContent.Create(body) };
        message.Headers.Add("access_token", token);
        using var response = await httpFactory.CreateClient(ZaloTokenProvider.HttpClientName).SendAsync(message, ct);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>(ct);
        return envelope ?? throw new JsonException("Phản hồi ZNS rỗng.");
    }
}

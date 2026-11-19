using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Zalo;
using ELearning.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Infrastructure.Zalo;

public sealed class ZaloAuthException(string message) : Exception(message);

/// <summary>
/// Giữ access token của Official Account. Access token sống ~25 giờ; refresh token dùng một lần (mỗi lần đổi Zalo cấp cặp
/// mới), nên cặp mới được ghi ngay vào bảng SystemSettings (khóa <c>Zalo.OaToken</c>). Cấu hình <c>Zalo:RefreshToken</c>
/// chỉ dùng để khởi tạo; đổi giá trị đó thì hệ thống bỏ cặp cũ và dùng giá trị mới.
/// </summary>
internal sealed class ZaloTokenProvider(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpFactory,
    IOptions<ZaloOptions> options,
    TimeProvider time,
    ILogger<ZaloTokenProvider> logger) : IDisposable
{
    public const string SettingKey = "Zalo.OaToken";
    public const string HttpClientName = "zalo";
    private const string TokenUrl = "https://oauth.zaloapp.com/v4/oa/access_token";

    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private TokenState? _cached;

    private sealed record TokenState(string AccessToken, string RefreshToken, DateTime ExpiresAt, string Origin);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] JsonElement ExpiresIn,
        [property: JsonPropertyName("error_name")] string? ErrorName,
        [property: JsonPropertyName("error_reason")] string? ErrorReason);

    public void Dispose() => _gate.Dispose();

    public async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = time.GetUtcNow().UtcDateTime;
            var state = _cached ?? await LoadAsync(ct);
            var configured = options.Value.RefreshToken;
            if (!string.IsNullOrWhiteSpace(configured) && state is not null && state.Origin != configured)
            {
                state = null;
            }

            if (!forceRefresh && state is not null && state.ExpiresAt - Margin > now)
            {
                _cached = state;
                return state.AccessToken;
            }

            var refreshToken = state?.RefreshToken ?? configured;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ZaloAuthException("Chưa có refresh token của Zalo OA (Zalo:RefreshToken).");
            }

            var origin = string.IsNullOrWhiteSpace(configured) ? (state?.Origin ?? "") : configured;
            state = await RefreshAsync(refreshToken, origin, now, ct);
            _cached = state;
            await SaveAsync(state, now, ct);
            return state.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TokenState> RefreshAsync(string refreshToken, string origin, DateTime now, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new ZaloAuthException("Thiếu Zalo:AppId hoặc Zalo:SecretKey.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["app_id"] = settings.AppId,
                ["grant_type"] = "refresh_token",
            }),
        };
        request.Headers.Add("secret_key", settings.SecretKey);

        using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
        TokenResponse? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        }
        catch (JsonException)
        {
            // Phản hồi không phải JSON: xử lý như bị từ chối bên dưới
        }

        if (body is null || string.IsNullOrEmpty(body.AccessToken) || string.IsNullOrEmpty(body.RefreshToken))
        {
            // Không ghi refresh token / secret vào log
            logger.LogError("Đổi token Zalo OA thất bại: HTTP {Status}, {Error} {Reason}", (int)response.StatusCode, body?.ErrorName, body?.ErrorReason);
            throw new ZaloAuthException($"Zalo từ chối đổi token ({body?.ErrorName ?? response.StatusCode.ToString()}). Cần lấy lại refresh token của OA.");
        }

        var seconds = body.ExpiresIn.ValueKind switch
        {
            JsonValueKind.Number => body.ExpiresIn.GetInt32(),
            JsonValueKind.String when int.TryParse(body.ExpiresIn.GetString(), out var n) => n,
            _ => 3600,
        };
        return new TokenState(body.AccessToken, body.RefreshToken, now.AddSeconds(seconds), origin);
    }

    private async Task<TokenState?> LoadAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var json = await db.SystemSettings.AsNoTracking().Where(s => s.Key == SettingKey).Select(s => s.Value).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<TokenState>(json);
    }

    private async Task SaveAsync(TokenState state, DateTime now, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var json = JsonSerializer.Serialize(state);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (setting is null)
        {
            db.SystemSettings.Add(new SystemSetting(SettingKey, json, "Token Zalo OA (bí mật, không hiển thị)", now, null));
        }
        else
        {
            setting.UpdateValue(json, now, null);
        }

        await db.SaveChangesAsync(ct);
    }
}

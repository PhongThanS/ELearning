using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ELearning.Api.Extensions;

namespace ELearning.ApiTests;

public sealed record Envelope<T>(bool Success, T? Data, string? Message, List<EnvelopeError> Errors, string? TraceId);

public sealed record EnvelopeError(string? Field, string Code, string Message);

public sealed record LoginData(string AccessToken, DateTime ExpiresAt, LoginUser User);

public sealed record LoginUser(Guid Id, string UserName, string FullName, List<string> Roles, List<string> Permissions, bool MustChangePassword);

public static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = CreateJson();

    public static async Task<(HttpResponseMessage Response, Envelope<T> Body)> SendJsonAsync<T>(
        this HttpClient client, HttpMethod method, string url, object? body = null, bool csrf = false, string? refreshCookie = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (csrf)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        }

        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var envelope = string.IsNullOrEmpty(text)
            ? new Envelope<T>(response.IsSuccessStatusCode, default, null, [], null)
            : JsonSerializer.Deserialize<Envelope<T>>(text, Json)!;
        return (response, envelope);
    }

    public static Task<(HttpResponseMessage Response, Envelope<T> Body)> GetJsonAsync<T>(this HttpClient client, string url) =>
        client.SendJsonAsync<T>(HttpMethod.Get, url);

    public static Task<(HttpResponseMessage Response, Envelope<T> Body)> PostJsonAsync<T>(
        this HttpClient client, string url, object? body = null, bool csrf = false, string? refreshCookie = null) =>
        client.SendJsonAsync<T>(HttpMethod.Post, url, body, csrf, refreshCookie);

    public const string RefreshCookieName = "elearning_rt";

    /// <summary>Đăng nhập và gắn access token vào client; trả về dữ liệu đăng nhập.</summary>
    public static async Task<LoginData> LoginAsync(this HttpClient client, string userName, string password) =>
        (await client.LoginWithCookieAsync(userName, password)).Login;

    /// <summary>Đăng nhập; trả về cả giá trị refresh cookie để test gửi lại thủ công.</summary>
    public static async Task<(LoginData Login, string RefreshCookie)> LoginWithCookieAsync(
        this HttpClient client, string userName, string password)
    {
        var (response, body) = await client.PostJsonAsync<LoginData>("/api/auth/login", new { userName, password });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Login {userName} thất bại: {(int)response.StatusCode} {body.Errors.FirstOrDefault()?.Code}");
        }

        client.UseToken(body.Data!.AccessToken);
        return (body.Data!, ReadRefreshCookie(response) ?? throw new InvalidOperationException("Thiếu refresh cookie."));
    }

    /// <summary>Giá trị cookie refresh trong Set-Cookie của response (null nếu không có hoặc bị xóa).</summary>
    public static string? ReadRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var header = cookies.FirstOrDefault(c => c.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal));
        var value = header?[(RefreshCookieName.Length + 1)..].Split(';')[0];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static void UseToken(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public static string UniqueName(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 50)];

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiServiceCollectionExtensions.ConfigureJson(options);
        return options;
    }
}

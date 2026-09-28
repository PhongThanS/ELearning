using ELearning.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace ELearning.Api.Security;

/// <summary>
/// Refresh token trong cookie HttpOnly, Secure, SameSite=Strict, Path=/api/auth (D-15).
/// Endpoint dùng cookie còn phải qua kiểm tra CSRF (docs/07-bao-mat.md mục 3.3).
/// </summary>
public sealed class RefreshTokenCookie(IOptions<AppOptions> appOptions)
{
    public const string CookieName = "elearning_rt";
    public const string CookiePath = "/api/auth";
    public const string CsrfHeaderName = "X-Requested-With";
    public const string CsrfHeaderValue = "XMLHttpRequest";

    public static string? Read(HttpRequest request) => request.Cookies.TryGetValue(CookieName, out var value) ? value : null;

    public static void Write(HttpResponse response, string token, DateTime expiresAt) =>
        response.Cookies.Append(CookieName, token, BuildOptions(expiresAt));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, BuildOptions(DateTime.UnixEpoch));

    /// <summary>Header X-Requested-With bắt buộc; nếu có Origin thì phải là origin frontend hoặc chính host API.</summary>
    public bool PassesCsrfCheck(HttpRequest request)
    {
        if (!string.Equals(request.Headers[CsrfHeaderName], CsrfHeaderValue, StringComparison.Ordinal))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        var self = $"{request.Scheme}://{request.Host}";
        return string.Equals(origin, appOptions.Value.PublicOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(origin, self, StringComparison.OrdinalIgnoreCase);
    }

    private static CookieOptions BuildOptions(DateTime expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
        IsEssential = true,
    };
}

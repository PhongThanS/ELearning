using System.Diagnostics;
using ELearning.Application.Common.Abstractions;
using ELearning.Infrastructure.Security;

namespace ELearning.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirst(AuthClaimTypes.Subject)?.Value, out var id) ? id : null;

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated == true;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public string? TraceId => Activity.Current?.Id ?? Context?.TraceIdentifier;
}

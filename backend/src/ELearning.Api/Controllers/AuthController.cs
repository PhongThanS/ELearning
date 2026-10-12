using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Auth;
using ELearning.Application.Common.Abstractions;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ELearning.Api.Controllers;

/// <summary>Xác thực (docs/05-api.md mục 6.1).</summary>
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser, RefreshTokenCookie cookie) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    [ProducesResponseType<ApiResponse<AuthUserDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        ToResponse(await auth.RegisterAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Login(LoginRequest request, CancellationToken ct) =>
        SessionResponse(await auth.LoginAsync(request, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthRefresh)]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Refresh(CancellationToken ct)
    {
        if (!cookie.PassesCsrfCheck(Request))
        {
            return CsrfFailure();
        }

        var result = await auth.RefreshAsync(RefreshTokenCookie.Read(Request), ct);
        if (result.IsFailure)
        {
            RefreshTokenCookie.Clear(Response);
        }

        return SessionResponse(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        if (!cookie.PassesCsrfCheck(Request))
        {
            return CsrfFailure();
        }

        await auth.LogoutAsync(RefreshTokenCookie.Read(Request), ct);
        RefreshTokenCookie.Clear(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<ApiResponse<AuthUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Me(CancellationToken ct) =>
        ToResponse(await auth.GetMeAsync(currentUser.RequiredUserId, ct));

    [HttpPost("change-password")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        SessionResponse(await auth.ChangePasswordAsync(currentUser.RequiredUserId, request, ct));

    /// <summary>MVP: không gửi email; luôn trả 200 để không lộ email có tồn tại hay không.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public ActionResult ForgotPassword() =>
        Ok(new ApiResponse<object>(
            true, null, "Vui lòng liên hệ quản trị viên để được đặt lại mật khẩu.", [], TraceId));

    private ActionResult SessionResponse(Result<AuthSession> result)
    {
        if (result.IsFailure)
        {
            return Failure(result);
        }

        RefreshTokenCookie.Write(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(ApiResponse.Ok(AuthResponse.From(result.Value), TraceId));
    }

    private ObjectResult CsrfFailure() =>
        StatusCode(
            StatusCodes.Status403Forbidden,
            ApiResponse.Fail(new Error(ErrorType.Forbidden, ErrorCodes.CsrfCheckFailed, "Request không hợp lệ."), TraceId));
}

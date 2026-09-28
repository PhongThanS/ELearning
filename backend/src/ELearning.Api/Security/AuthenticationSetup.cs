using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ELearning.Api.Security;

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinimumKeyBytes,
                $"Jwt:SigningKey phải dài tối thiểu {JwtOptions.MinimumKeyBytes} byte.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, time) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaimTypes.Name,
                    RoleClaimType = AuthClaimTypes.Role,

                    // Kiểm tra hạn token bằng cùng TimeProvider với phần phát token
                    // (một nguồn thời gian duy nhất; FakeTimeProvider trong test).
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = time.GetUtcNow().UtcDateTime;
                        return (notBefore is null || notBefore.Value <= now + parameters.ClockSkew)
                            && expires is not null && expires.Value >= now - parameters.ClockSkew;
                    },
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = ValidateUserStatusAsync };
            });

        return services;
    }

    /// <summary>
    /// Thu hồi gần như tức thời (docs/07-bao-mat.md mục 3.4): user phải còn hoạt động
    /// và SecurityStamp trong token phải trùng giá trị hiện tại.
    /// </summary>
    private static async Task ValidateUserStatusAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(AuthClaimTypes.Subject)?.Value, out var userId))
        {
            context.Fail("Token thiếu sub.");
            return;
        }

        var access = await context.HttpContext.RequestServices.GetRequiredService<IUserAccessService>()
            .GetAsync(userId, context.HttpContext.RequestAborted);
        var stamp = principal!.FindFirst(AuthClaimTypes.SecurityStamp)?.Value;
        if (access is null || !access.IsActive || !string.Equals(stamp, access.SecurityStamp.ToString("N"), StringComparison.Ordinal))
        {
            context.Fail("Tài khoản đã bị vô hiệu hóa hoặc phiên đã bị thu hồi.");
        }
    }
}

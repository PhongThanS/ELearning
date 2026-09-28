using System.Diagnostics;
using ELearning.Api.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Identity;
using ELearning.Infrastructure.Security;
using ELearning.Shared;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ELearning.Api.Security;

/// <summary>[HasPermission(Permissions.ExamPublish)] — mỗi permission là một policy (D-14).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(permission)
{
    public string Permission { get; } = permission;
}

public static class Policies
{
    public const string StudentOnly = "StudentOnly";
}

internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

internal sealed class StudentRequirement : IAuthorizationRequirement;

/// <summary>Lý do từ chối riêng để trả mã lỗi cụ thể.</summary>
internal sealed class PasswordChangeRequiredReason(IAuthorizationHandler handler)
    : AuthorizationFailureReason(handler, "Password change required");

/// <summary>Kiểm tra permission / vai trò từ ảnh chụp quyền đã cache, không tin claim trong token.</summary>
internal sealed class AccessAuthorizationHandler(IUserAccessService userAccess) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements
            .Where(r => r is PermissionRequirement or StudentRequirement)
            .ToList();
        if (pending.Count == 0)
        {
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst(AuthClaimTypes.Subject)?.Value, out var userId)
            || await userAccess.GetAsync(userId) is not { IsActive: true } access)
        {
            return;
        }

        if (access.MustChangePassword)
        {
            context.Fail(new PasswordChangeRequiredReason(this));
            return;
        }

        foreach (var requirement in pending)
        {
            var satisfied = requirement switch
            {
                PermissionRequirement p => access.Permissions.Contains(p.Permission),
                StudentRequirement => access.Roles.Contains(Role.StudentCode),
                _ => false,
            };
            if (satisfied)
            {
                context.Succeed(requirement);
            }
        }
    }
}

/// <summary>403 do phải đổi mật khẩu → trả mã PASSWORD_CHANGE_REQUIRED; còn lại để StatusCodePages ghi body.</summary>
internal sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.OfType<PasswordChangeRequiredReason>().Any() == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var error = new Error(ErrorType.Forbidden, ErrorCodes.PasswordChangeRequired, "Bạn cần đổi mật khẩu trước khi tiếp tục.");
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(error, Activity.Current?.Id ?? context.TraceIdentifier));
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

public static class AuthorizationSetup
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.StudentOnly, p => p.RequireAuthenticatedUser().AddRequirements(new StudentRequirement()));

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All.Keys)
            {
                options.AddPolicy(permission, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
            }
        });

        services.AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        return services;
    }
}

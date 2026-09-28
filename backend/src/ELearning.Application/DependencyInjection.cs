using ELearning.Application.Audit;
using ELearning.Application.Auth;
using ELearning.Application.Groups;
using ELearning.Application.Questions;
using ELearning.Application.Roles;
using ELearning.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IQuestionService, QuestionService>();
        return services;
    }
}

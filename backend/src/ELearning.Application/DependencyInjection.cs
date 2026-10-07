using ELearning.Application.Admin;
using ELearning.Application.Attempts;
using ELearning.Application.Audit;
using ELearning.Application.Auth;
using ELearning.Application.Classes;
using ELearning.Application.Exams;
using ELearning.Application.Grading;
using ELearning.Application.Groups;
using ELearning.Application.Media;
using ELearning.Application.Monitoring;
using ELearning.Application.Questions;
using ELearning.Application.Roles;
using ELearning.Application.Users;
using ELearning.Domain.Grading;
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
        services.AddScoped<IClassroomService, ClassroomService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IQuestionImportService, QuestionImportService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamVersionService, ExamVersionService>();

        services.AddSingleton(GradingEngine.Default);
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IAttemptFinalizer, AttemptFinalizer>();
        services.AddScoped<IAttemptService, AttemptService>();
        services.AddScoped<AttemptExpirationService>();
        services.AddScoped<IAttemptExpirationService>(sp => sp.GetRequiredService<AttemptExpirationService>());
        services.AddScoped<IExamAttemptCloser>(sp => sp.GetRequiredService<AttemptExpirationService>());
        services.AddScoped<IAdminAttemptService, AdminAttemptService>();
        services.AddScoped<IResultAdminService, ResultAdminService>();
        services.AddScoped<IAnswerKeyService, AnswerKeyService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IManualGradingService, ManualGradingService>();

        services.AddSingleton<MediaLinks>();
        services.AddScoped<IMediaService, MediaService>();

        // IMeterFactory do host đăng ký (AddMetrics)
        services.AddSingleton<OperationalMetrics>();
        services.AddScoped<IAttemptBacklogQuery, AttemptBacklogQuery>();
        return services;
    }
}

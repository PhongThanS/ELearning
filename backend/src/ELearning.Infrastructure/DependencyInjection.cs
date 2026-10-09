using ELearning.Application.Admin;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Media;
using ELearning.Application.Questions;
using ELearning.Infrastructure.BackgroundJobs;
using ELearning.Infrastructure.Excel;
using ELearning.Infrastructure.Media;
using ELearning.Infrastructure.Persistence;
using ELearning.Infrastructure.Persistence.Seed;
using ELearning.Infrastructure.Queries;
using ELearning.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ELearning.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Đọc connection string khi resolve (không phải lúc đăng ký) để test có thể ghi đè cấu hình.
        services.AddDbContext<ELearningDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Thiếu ConnectionStrings:{ConnectionStringName}. Cấu hình bằng user-secrets hoặc biến môi trường.");
            }

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
            });
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ELearningDbContext>());
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IAttemptLock, AttemptLock>();
        services.AddScoped<IReportQuery, ReportQuery>();
        services.AddSingleton<IResultExporter, ResultExporter>();
        services.AddSingleton<IQuestionImportWorkbook, QuestionImportWorkbook>();
        services.AddSingleton<IMediaStore, FileSystemMediaStore>();
        services.AddHostedService<AttemptExpirationWorker>();
        services.AddHostedService<MonitoringSnapshotWorker>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IUserAccessService, UserAccessService>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}

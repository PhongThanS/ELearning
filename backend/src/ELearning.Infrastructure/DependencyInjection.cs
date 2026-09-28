using ELearning.Application.Common.Abstractions;
using ELearning.Application.Questions;
using ELearning.Infrastructure.Persistence;
using ELearning.Infrastructure.Persistence.Seed;
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

            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3);
                sql.MigrationsHistoryTable("__EFMigrationsHistory");
            });
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ELearningDbContext>());
        services.AddScoped<ICodeGenerator, CodeGenerator>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IUserAccessService, UserAccessService>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}

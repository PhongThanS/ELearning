using ELearning.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ELearning.ApiTests;

/// <summary>
/// Chạy API thật (môi trường Development, có seed tài khoản demo) trên database SQL Server tạm.
/// Đồng hồ là FakeTimeProvider để test ân hạn / hết hạn.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Admin@123456";
    public const string StudentUserName = "student01";
    public const string StudentPassword = "Student@123456";

    private readonly SqlServerTestDatabase _database = new();

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Giới hạn login mỗi phút; test rate limit dùng factory riêng với giá trị thấp.</summary>
    protected virtual int LoginRateLimitPerMinute => 10_000;

    public async Task InitializeAsync() => await _database.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>Client HTTPS; cookie refresh token được test tự quản lý (đọc Set-Cookie, gửi header Cookie).</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DevPassword", StudentPassword);
        builder.UseSetting("RateLimits:AuthLoginPerMinute", LoginRateLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimits:AuthRefreshPerMinute", "10000");
        builder.UseSetting("RateLimits:DefaultPerMinute", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

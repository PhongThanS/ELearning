using ELearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ELearning.TestSupport;

/// <summary>
/// Database PostgreSQL thật cho integration / API test (docs/08-kiem-thu.md mục 1).
/// - Nếu có biến môi trường ELEARNING_TEST_POSTGRES (connection string tới PostgreSQL server):
///   tạo database tạm tên ngẫu nhiên trên server đó.
/// - Nếu không: khởi động PostgreSQL bằng Testcontainers (cần Docker, ví dụ trên CI).
/// Database được áp migration thật và bị xóa khi test kết thúc.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public const string ServerConnectionVariable = "ELEARNING_TEST_POSTGRES";

    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable(ServerConnectionVariable);
        if (string.IsNullOrWhiteSpace(serverConnection))
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            serverConnection = _container.GetConnectionString();
        }

        var builder = new NpgsqlConnectionStringBuilder(serverConnection)
        {
            Database = $"elearning_test_{Guid.NewGuid():N}",
        };
        ConnectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        NpgsqlConnection.ClearAllPools();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public ELearningDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ELearningDbContext>().UseNpgsql(ConnectionString).Options);
}

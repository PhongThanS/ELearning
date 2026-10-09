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
    private string? _serverConnection;
    private string? _databaseName;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _serverConnection = Environment.GetEnvironmentVariable(ServerConnectionVariable);
        if (string.IsNullOrWhiteSpace(_serverConnection))
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            await _container.StartAsync();
            _serverConnection = _container.GetConnectionString();
        }

        _databaseName = $"elearning_test_{Guid.NewGuid():N}";

        await using (var adminConn = new NpgsqlConnection(_serverConnection))
        {
            await adminConn.OpenAsync();
            await using var cmd = adminConn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{_databaseName}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_serverConnection)
        {
            Database = _databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        if (!string.IsNullOrWhiteSpace(_serverConnection) && !string.IsNullOrWhiteSpace(_databaseName))
        {
            try
            {
                await using var adminConn = new NpgsqlConnection(_serverConnection);
                await adminConn.OpenAsync();
                await using var cmd = adminConn.CreateCommand();
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);";
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Bỏ qua lỗi xóa db khi kết thúc test
            }
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public ELearningDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ELearningDbContext>().UseNpgsql(ConnectionString).Options);
}

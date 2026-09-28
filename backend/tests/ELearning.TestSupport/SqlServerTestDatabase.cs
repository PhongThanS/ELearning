using ELearning.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace ELearning.TestSupport;

/// <summary>
/// Database SQL Server thật cho integration / API test (docs/08-kiem-thu.md mục 1).
/// - Nếu có biến môi trường ELEARNING_TEST_SQL (connection string tới một SQL Server, không cần Database):
///   tạo database tạm tên ngẫu nhiên trên server đó.
/// - Nếu không: khởi động SQL Server bằng Testcontainers (cần Docker, ví dụ trên CI).
/// Database được áp migration thật và bị xóa khi test kết thúc.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public const string ServerConnectionVariable = "ELEARNING_TEST_SQL";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable(ServerConnectionVariable);
        if (string.IsNullOrWhiteSpace(serverConnection))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            serverConnection = _container.GetConnectionString();
        }

        var builder = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = $"ELearningTest_{Guid.NewGuid():N}",
            TrustServerCertificate = true,
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

        SqlConnection.ClearAllPools();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public ELearningDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ELearningDbContext>().UseSqlServer(ConnectionString).Options);
}

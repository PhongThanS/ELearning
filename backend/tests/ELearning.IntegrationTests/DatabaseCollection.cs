using ELearning.TestSupport;

namespace ELearning.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerTestDatabase>
{
    public const string Name = "Database";
}

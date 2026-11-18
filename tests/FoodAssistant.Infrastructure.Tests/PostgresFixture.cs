using FoodAssistant.Infrastructure.Tests;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>One PostgreSQL container for the whole test assembly; every test uses its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Connection string to a fresh, not yet existing database (migrations create it).</summary>
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"t_{Guid.NewGuid():N}",
        }.ConnectionString;

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

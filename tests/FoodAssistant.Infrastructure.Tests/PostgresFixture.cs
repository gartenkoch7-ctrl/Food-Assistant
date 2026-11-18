using FoodAssistant.Infrastructure.Tests;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>One PostgreSQL container for the whole test assembly; every test uses its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        // Many tests run in parallel, each with its own database and connection pool.
        .WithCommand("-c", "max_connections=500")
        .Build();

    /// <summary>Connection string to a fresh, not yet existing database (migrations create it).</summary>
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"t_{Guid.NewGuid():N}",
            // The full solution run starts several containers at once; the 15 s default was too short.
            Timeout = 60,
        }.ConnectionString;

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

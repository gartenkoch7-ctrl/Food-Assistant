using FoodAssistant.Server.Tests.Auth;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>One PostgreSQL container for all server tests; each test app gets its own database.</summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        // Many tests run in parallel, each with its own database and connection pool.
        .WithCommand("-c", "max_connections=500")
        .Build();

    /// <summary>Connection string to a fresh database; the server's startup migration creates it.</summary>
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

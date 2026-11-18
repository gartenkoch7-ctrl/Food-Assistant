using FoodAssistant.Server.Tests.Auth;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>One PostgreSQL container for all server tests; each test app gets its own database.</summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Connection string to a fresh database; the server's startup migration creates it.</summary>
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"t_{Guid.NewGuid():N}",
        }.ConnectionString;

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

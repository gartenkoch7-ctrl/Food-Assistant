using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Tests;

public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task StartupMigration_CreatesSchema_AndModelMatchesMigrations()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var ct = TestContext.Current.CancellationToken;

        var applied = await scope.Db.Database.GetAppliedMigrationsAsync(ct);
        var pending = await scope.Db.Database.GetPendingMigrationsAsync(ct);

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
        Assert.False(scope.Db.Database.HasPendingModelChanges(), "The model has changes without a migration.");
    }

    [Fact]
    public async Task StartupMigration_IsIdempotent()
    {
        var connectionString = postgres.NewDatabaseConnectionString();
        await using (await TestDatabase.CreateAsync(connectionString))
        {
        }

        await using var second = await TestDatabase.CreateAsync(connectionString);
        await using var scope = second.CreateScope();
        Assert.Empty(await scope.Db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartupMigration_UnreachableDatabase_FailsWithClearMessage()
    {
        var unreachable = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "none",
            Username = "none",
            Password = "none",
            Timeout = 2,
        }.ConnectionString;
        await using var database = await TestDatabase.CreateAsync(unreachable, migrate: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(database.RunStartupMigrationAsync);

        Assert.Contains("migration failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartupMigration_MissingConnectionString_FailsWithClearMessage()
    {
        await using var database = await TestDatabase.CreateAsync(string.Empty, migrate: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(database.RunStartupMigrationAsync);

        Assert.Contains("ConnectionStrings__FoodAssistant", exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schema_UsesXminAsRowVersion()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();

        var version = scope.Db.Model.FindEntityType(typeof(Households.Household))!.FindProperty(nameof(Households.Household.Version))!;

        Assert.Equal("xmin", version.GetColumnName());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(typeof(AppDbContext), scope.Db.GetType());
    }
}

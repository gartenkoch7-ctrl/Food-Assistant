using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Composition;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>
/// The real infrastructure registration against a fresh database. Runs the startup migration
/// exactly like the server does.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly IHost _host;

    private TestDatabase(IHost host) => _host = host;

    public IServiceProvider Services => _host.Services;

    public static async Task<TestDatabase> CreateAsync(string connectionString, bool migrate = true)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[$"ConnectionStrings:{PersistenceRegistration.ConnectionStringName}"] = connectionString;
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<TestHouseholdContext>();
        builder.Services.AddScoped<IHouseholdContext>(sp => sp.GetRequiredService<TestHouseholdContext>());
        builder.AddInfrastructure();

        var database = new TestDatabase(builder.Build());
        if (migrate)
        {
            await database.RunStartupMigrationAsync().ConfigureAwait(false);
        }

        return database;
    }

    /// <summary>Starts the hosted migration service only (no other background work).</summary>
    public async Task RunStartupMigrationAsync()
    {
        var migrator = Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "DatabaseMigrationService");
        await migrator.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>A new request-like scope with its own context and household.</summary>
    public Scope CreateScope(Guid? activeHousehold = null)
    {
        var scope = Services.CreateAsyncScope();
        var household = scope.ServiceProvider.GetRequiredService<TestHouseholdContext>();
        if (activeHousehold is { } id)
        {
            household.Activate(id);
        }

        return new Scope(scope, scope.ServiceProvider.GetRequiredService<AppDbContext>(), household);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _host.Dispose();
        }
    }

    public sealed record Scope(AsyncServiceScope ServiceScope, AppDbContext Db, TestHouseholdContext Household)
        : IAsyncDisposable
    {
        public T Get<T>() where T : notnull => ServiceScope.ServiceProvider.GetRequiredService<T>();

        public ValueTask DisposeAsync() => ServiceScope.DisposeAsync();
    }
}

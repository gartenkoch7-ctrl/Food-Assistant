using FoodAssistant.Application.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations at startup, before the server accepts requests (ADR-0007).
/// If a migration fails, the exception stops the host: the server must not run on a half-migrated
/// or unreachable database.
/// </summary>
internal sealed class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.persistence.migrate");

        // TODO(M1): take a database backup before migrating and name it in the failure log, so
        // the self-hoster has a way back (ADR-0007, "Updates und Migrationen").
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            activity?.SetTag("foodassistant.migrations.pending", pending.Count);
            if (pending.Count == 0)
            {
                PersistenceLog.DatabaseUpToDate(logger);
                return;
            }

            PersistenceLog.MigrationsStarting(logger, pending.Count);
            var started = timeProvider.GetTimestamp();
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
            PersistenceLog.MigrationsApplied(logger, pending.Count, elapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, exception.GetType().Name);
            PersistenceLog.MigrationFailed(logger, exception);
            throw new InvalidOperationException(
                "Database migration failed, the server does not start. Check ConnectionStrings__FoodAssistant "
                + "and that PostgreSQL is reachable; details are in the previous log entry.",
                exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

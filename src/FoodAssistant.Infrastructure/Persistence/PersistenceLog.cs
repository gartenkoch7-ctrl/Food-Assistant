using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>Log events of persistence (Infrastructure range 2000–2199, block 2000–2019).</summary>
internal static partial class PersistenceLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Database schema is up to date")]
    public static partial void DatabaseUpToDate(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Applying {Count} database migration(s)")]
    public static partial void MigrationsStarting(ILogger logger, int count);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Applied {Count} database migration(s) in {ElapsedMilliseconds} ms")]
    public static partial void MigrationsApplied(ILogger logger, int count, long elapsedMilliseconds);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Critical,
        Message = "Database migration failed; the server stops. Restore the backup or fix the database and restart")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);
}

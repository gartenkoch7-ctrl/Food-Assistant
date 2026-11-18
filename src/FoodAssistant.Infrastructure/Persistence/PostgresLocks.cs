using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped PostgreSQL advisory locks for check-then-act rules that a row version cannot
/// protect (counting rows). Must be called inside a transaction; released on commit or rollback.
/// </summary>
internal static class PostgresLocks
{
    // Arbitrary constant; only needs to differ from other global lock keys.
    private const long SetupLockKey = 0x46_41_53_45_54_55_50; // "FASETUP"

    /// <summary>Serializes the setup so two concurrent setups cannot both find "no account yet".</summary>
    public static Task LockSetupAsync(DatabaseFacade database, CancellationToken cancellationToken) =>
        database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SetupLockKey})", cancellationToken);

    /// <summary>Serializes membership changes of one household (e.g. the last-owner rule).</summary>
    public static Task LockHouseholdAsync(DatabaseFacade database, Guid householdId, CancellationToken cancellationToken) =>
        database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({householdId.ToString()}, 0))",
            cancellationToken);
}

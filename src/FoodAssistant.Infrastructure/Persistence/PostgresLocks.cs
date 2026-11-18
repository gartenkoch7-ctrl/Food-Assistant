using System.Data.Common;
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

    /// <summary>
    /// Serializes change-log writes of one household until the end of <paramref name="transaction"/>,
    /// so its change-log sequences are assigned in commit order (see <c>SyncChangeInterceptor</c>).
    /// Works on the raw transaction because the sync interceptors also call it while EF Core is
    /// still starting its own transaction.
    /// </summary>
    public static async Task LockHouseholdChangeLogAsync(
        DbTransaction transaction,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var command = transaction.Connection!.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;

            // Seed 1 keeps this lock apart from LockHouseholdAsync (seed 0) of the same household.
            command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@household, 1))";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "household";
            parameter.Value = householdId.ToString();
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

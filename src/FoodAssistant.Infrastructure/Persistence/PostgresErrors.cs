using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>Classification of PostgreSQL errors surfaced through EF Core.</summary>
internal static class PostgresErrors
{
    /// <summary>A unique index was violated (SQLSTATE 23505), e.g. by a concurrent insert.</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

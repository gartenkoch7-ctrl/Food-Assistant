using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>Classification of save errors the shopping stores expect under concurrency.</summary>
internal static class ShoppingDbErrors
{
    /// <summary>A referenced row (list, store) was deleted between the check and the save (SQLSTATE 23503).</summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    /// <summary>An id is taken, or a referenced row is gone.</summary>
    public static bool IsKeyConflict(DbUpdateException exception) =>
        PostgresErrors.IsUniqueViolation(exception) || IsForeignKeyViolation(exception);
}

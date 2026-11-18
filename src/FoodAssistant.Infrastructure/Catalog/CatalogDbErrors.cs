using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>PostgreSQL errors the catalog store answers with a conflict instead of a failure.</summary>
internal static class CatalogDbErrors
{
    /// <summary>A row still referenced elsewhere was deleted, or a reference points nowhere (SQLSTATE 23503).</summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    /// <summary>The unique index of <c>ProductBarcodes</c> (household + code) was violated, e.g. by a concurrent insert.</summary>
    public static bool IsBarcodeViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "ProductBarcodes" };
}

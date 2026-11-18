using System.Text.Json;
using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>
/// Server-wide Open Food Facts cache. <see cref="FoodFactsCacheEntry"/> is not household data, so
/// no household filter applies (docs/kb/externe-daten.md).
/// </summary>
internal sealed class FoodFactsCacheStore(AppDbContext db) : IFoodFactsCache
{
    private static readonly TimeSpan UnusedRetention = TimeSpan.FromDays(90);

    public async Task<FoodFactsCacheHit?> GetAsync(string barcode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = await db.Set<FoodFactsCacheEntry>()
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Barcode == barcode, cancellationToken)
            .ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        await db.Set<FoodFactsCacheEntry>()
            .Where(e => e.Barcode == barcode)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastRequestedAt, now), cancellationToken)
            .ConfigureAwait(false);

        var product = entry is { Found: true, PayloadJson: not null }
            ? JsonSerializer.Deserialize<FoodFactsProduct>(entry.PayloadJson)
            : null;

        // A found entry without readable data is useless; treat it as absent so it is fetched again.
        return entry.Found && product is null
            ? null
            : new FoodFactsCacheHit(
                entry.Found ? new FoodFactsLookup(FoodFactsStatus.Found, product) : FoodFactsLookup.NotFound,
                entry.ExpiresAt);
    }

    public async Task StoreAsync(
        string barcode,
        FoodFactsLookup lookup,
        DateTimeOffset fetchedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (lookup.Status == FoodFactsStatus.Unavailable)
        {
            return;
        }

        var found = lookup.Status == FoodFactsStatus.Found;
        var payload = found ? JsonSerializer.Serialize(lookup.Product) : null;

        // One statement: two requests for the same new barcode must not fail on the primary key.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "FoodFactsCache" ("Barcode", "Found", "PayloadJson", "FetchedAt", "ExpiresAt", "LastRequestedAt")
            VALUES ({barcode}, {found}, CAST({payload} AS jsonb), {fetchedAt.ToUniversalTime()}, {expiresAt.ToUniversalTime()}, {fetchedAt.ToUniversalTime()})
            ON CONFLICT ("Barcode") DO UPDATE SET
                "Found" = EXCLUDED."Found",
                "PayloadJson" = EXCLUDED."PayloadJson",
                "FetchedAt" = EXCLUDED."FetchedAt",
                "ExpiresAt" = EXCLUDED."ExpiresAt",
                "LastRequestedAt" = EXCLUDED."LastRequestedAt"
            """,
            cancellationToken).ConfigureAwait(false);

        // Nobody asked for these for a long time: drop them, so the table cannot become a record
        // of what a household scanned (docs/kb/datenschutz.md).
        var cutoff = fetchedAt - UnusedRetention;
        await db.Set<FoodFactsCacheEntry>()
            .Where(e => e.LastRequestedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

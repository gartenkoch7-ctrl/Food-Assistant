namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>
/// Cached Open Food Facts answer for one barcode. Deliberately server-wide and not
/// <c>IHouseholdOwned</c>: it holds only public product data, never household data, and saves
/// repeated lookups (docs/kb/externe-daten.md).
/// </summary>
public sealed class FoodFactsCacheEntry
{
    public required string Barcode { get; init; }

    /// <summary>False caches "unknown product" so misses are not asked again before <see cref="ExpiresAt"/>.</summary>
    public bool Found { get; set; }

    /// <summary>Reduced product data as returned to clients; null when not found.</summary>
    public string? PayloadJson { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Last lookup by any household; used to drop entries nobody asks for any more.</summary>
    public DateTimeOffset LastRequestedAt { get; set; }
}

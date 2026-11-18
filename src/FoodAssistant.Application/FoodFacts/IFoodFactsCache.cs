namespace FoodAssistant.Application.FoodFacts;

/// <summary>Server-wide cache of lookup answers, including "not found". Holds no household data.</summary>
public interface IFoodFactsCache
{
    /// <summary>The entry for <paramref name="barcode"/> (also expired ones) and marks it as requested at <paramref name="now"/>.</summary>
    Task<FoodFactsCacheHit?> GetAsync(string barcode, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the entry; only <see cref="FoodFactsStatus.Found"/> and <see cref="FoodFactsStatus.NotFound"/> are stored.</summary>
    Task StoreAsync(
        string barcode,
        FoodFactsLookup lookup,
        DateTimeOffset fetchedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

/// <summary>A cached lookup and when it stops being fresh.</summary>
public sealed record FoodFactsCacheHit(FoodFactsLookup Lookup, DateTimeOffset ExpiresAt);

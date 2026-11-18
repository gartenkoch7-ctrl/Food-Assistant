namespace FoodAssistant.Application.FoodFacts;

/// <summary>Result of a lookup; <paramref name="Stale"/> is true for an expired cache entry served after a failed refresh.</summary>
public sealed record FoodFactsLookup(FoodFactsStatus Status, FoodFactsProduct? Product, bool Stale = false)
{
    public static FoodFactsLookup NotFound { get; } = new(FoodFactsStatus.NotFound, null);

    public static FoodFactsLookup Unavailable { get; } = new(FoodFactsStatus.Unavailable, null);
}

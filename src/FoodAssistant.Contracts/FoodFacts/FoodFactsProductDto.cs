using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.FoodFacts;

/// <summary>
/// Product suggestion for a barcode, taken from Open Food Facts (community data, not verified).
/// <paramref name="SuggestedCategoryKey"/> is a key of the fixed category list; null when unknown.
/// <paramref name="Stale"/> marks an expired cache entry served because Open Food Facts was unreachable.
/// </summary>
public sealed record FoodFactsProductDto(
    string Barcode,
    string? Name,
    string? Brand,
    string? QuantityText,
    QuantityDto? PackageQuantity,
    string? SuggestedCategoryKey,
    bool Stale);

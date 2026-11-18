using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.FoodFacts;

namespace FoodAssistant.Application.FoodFacts;

/// <summary>Turns cached OFF data into the suggestion shown to the user.</summary>
public static class FoodFactsSuggestion
{
    public static FoodFactsProductDto ToDto(
        FoodFactsProduct product,
        string culture,
        IFoodFactsCategoryResolver categories,
        bool stale)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(categories);
        return new FoodFactsProductDto(
            product.Barcode,
            NameFor(product, culture),
            product.Brand,
            product.QuantityText,
            PackageQuantityOf(product),
            categories.CategoryOf(product.CategoryTags),
            stale);
    }

    // OFF has per-language names; prefer the request language, then the generic one.
    private static string? NameFor(FoodFactsProduct product, string? culture)
    {
        var language = culture?.Split('-', 2)[0].ToLowerInvariant();
        var localized = language switch
        {
            "de" => product.NameDe,
            "en" => product.NameEn,
            _ => null,
        };
        return localized ?? product.Name;
    }

    // Only units the app can calculate with; anything else (e.g. "cl", "oz") stays in QuantityText.
    private static QuantityDto? PackageQuantityOf(FoodFactsProduct product)
    {
        if (product.PackageQuantity is not > 0)
        {
            return null;
        }

        var unit = product.PackageUnit?.Trim().ToLowerInvariant();
        return unit is UnitCodes.Gram or UnitCodes.Kilogram or UnitCodes.Millilitre or UnitCodes.Litre
            ? new QuantityDto(product.PackageQuantity.Value, unit)
            : null;
    }
}

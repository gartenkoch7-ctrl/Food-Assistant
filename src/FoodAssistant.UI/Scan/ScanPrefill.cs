using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.FoodFacts;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Scan;

/// <summary>Start values of the "new product" dialog for a scanned barcode. Pure mapping.</summary>
public static class ScanPrefill
{
    /// <param name="barcode">The scanned barcode; always kept so the next scan finds the product.</param>
    /// <param name="suggestion">Open Food Facts data, or <see langword="null"/> when there is none (unknown, disabled, unreachable).</param>
    public static CreateProductRequest For(string barcode, FoodFactsProductDto? suggestion) =>
        suggestion is null
            ? new CreateProductRequest(null, string.Empty, null, string.Empty, UnitCodes.Piece, null, null, null, [barcode], null)
            : new CreateProductRequest(
                null,
                suggestion.Name ?? string.Empty,
                suggestion.Brand,
                suggestion.SuggestedCategoryKey ?? string.Empty,
                suggestion.PackageQuantity?.Unit ?? UnitCodes.Piece,
                null,
                null,
                null,
                [barcode],
                barcode);

    /// <summary>The note on top of the dialog: attribution when data was taken over, else a hint that nothing was found.</summary>
    public static RenderFragment Notice(bool suggestionFound) => builder =>
    {
        builder.OpenComponent<FoodFactsAttribution>(0);
        builder.AddAttribute(1, nameof(FoodFactsAttribution.Found), suggestionFound);
        builder.CloseComponent();
    };
}

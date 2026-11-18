namespace FoodAssistant.Application.FoodFacts;

/// <summary>Reduced Open Food Facts product data; this is what the server cache stores.</summary>
/// <param name="PackageUnit">Unit as OFF reports it (<c>product_quantity_unit</c>), e.g. <c>g</c> or <c>ml</c>.</param>
/// <param name="CategoryTags">OFF <c>categories_tags</c>, e.g. <c>en:cheeses</c>.</param>
public sealed record FoodFactsProduct(
    string Barcode,
    string? Name,
    string? NameDe,
    string? NameEn,
    string? Brand,
    string? QuantityText,
    decimal? PackageQuantity,
    string? PackageUnit,
    IReadOnlyList<string> CategoryTags);

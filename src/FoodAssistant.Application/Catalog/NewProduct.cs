using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Catalog;

/// <summary>A validated new product (names trimmed, keys and unit checked, barcodes distinct).</summary>
public sealed record NewProduct(
    Guid Id,
    Guid HouseholdId,
    string Name,
    string? Brand,
    string CategoryKey,
    Unit DefaultUnit,
    Guid? DefaultStorageLocationId,
    string? SectionKey,
    Quantity? MinimumStock,
    IReadOnlyList<string> Barcodes,
    string? FoodFactsBarcode,
    DateTimeOffset Now);

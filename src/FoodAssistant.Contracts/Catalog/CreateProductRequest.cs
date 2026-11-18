using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>
/// Creates a product. <paramref name="Id"/> is optional (UUIDv7 from the client, ADR-0004);
/// <paramref name="FoodFactsBarcode"/> marks data taken over from Open Food Facts.
/// </summary>
public sealed record CreateProductRequest(
    Guid? Id,
    string Name,
    string? Brand,
    string CategoryKey,
    string DefaultUnit,
    Guid? DefaultStorageLocationId,
    string? SectionKey,
    QuantityDto? MinimumStock,
    IReadOnlyList<string>? Barcodes,
    string? FoodFactsBarcode);

using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>Replaces the editable fields of a product; sent with <c>If-Match</c> (ADR-0004).</summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Brand,
    string CategoryKey,
    string DefaultUnit,
    Guid? DefaultStorageLocationId,
    string? SectionKey,
    QuantityDto? MinimumStock,
    IReadOnlyList<string>? Barcodes);

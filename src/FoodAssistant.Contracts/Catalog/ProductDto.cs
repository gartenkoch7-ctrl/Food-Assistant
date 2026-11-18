using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>
/// A product of the household catalog. <paramref name="CategoryKey"/> and <paramref name="SectionKey"/>
/// are keys of the fixed lists (<see cref="CategoryDto"/>); <paramref name="DefaultUnit"/> is a
/// <see cref="UnitCodes"/> value; <paramref name="Version"/> is also sent as <c>ETag</c> (ADR-0004).
/// </summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Brand,
    string CategoryKey,
    string DefaultUnit,
    Guid? DefaultStorageLocationId,
    string? SectionKey,
    QuantityDto? MinimumStock,
    IReadOnlyList<string> Barcodes,
    bool FromOpenFoodFacts,
    long Version);

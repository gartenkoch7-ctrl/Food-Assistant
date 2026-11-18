using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Catalog;

/// <summary>Validated replacement of the editable fields of a product.</summary>
public sealed record ProductChanges(
    string Name,
    string? Brand,
    string CategoryKey,
    Unit DefaultUnit,
    Guid? DefaultStorageLocationId,
    string? SectionKey,
    Quantity? MinimumStock,
    IReadOnlyList<string> Barcodes,
    DateTimeOffset Now);

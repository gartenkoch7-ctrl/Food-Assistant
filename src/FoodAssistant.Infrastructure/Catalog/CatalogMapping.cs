using System.Linq.Expressions;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>Entities to Contracts DTOs (EF entities never leave Infrastructure).</summary>
internal static class CatalogMapping
{
    public static Expression<Func<Product, ProductSummaryDto>> ToSummary { get; } = p =>
        new ProductSummaryDto(p.Id, p.Name, p.Brand, p.CategoryKey, p.DefaultUnitCode);

    /// <remarks>Needs <see cref="Product.Barcodes"/> loaded.</remarks>
    public static ProductDto ToDto(Product p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new ProductDto(
            p.Id,
            p.Name,
            p.Brand,
            p.CategoryKey,
            p.DefaultUnitCode,
            p.DefaultStorageLocationId,
            p.SectionKey,
            p.MinimumStockValue is { } value ? new QuantityDto(value, p.MinimumStockUnitCode!) : null,
            [.. p.Barcodes.Select(b => b.Code).Order(StringComparer.Ordinal)],
            p.FoodFactsBarcode is not null,
            p.Version);
    }

    public static StorageLocationDto ToDto(StorageLocation l)
    {
        ArgumentNullException.ThrowIfNull(l);

        // The contract enum mirrors the domain enum member by member (ContractEnumTests).
        return new StorageLocationDto(l.Id, l.Name, (StorageTypeDto)(int)l.Type, l.SortOrder, l.Version);
    }
}

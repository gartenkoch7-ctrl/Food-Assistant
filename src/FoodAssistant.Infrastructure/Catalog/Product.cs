using FoodAssistant.Domain.Expiry;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>A product of the household catalog. Products are archived, never deleted, so history keeps its names.</summary>
public sealed class Product : ISyncTracked
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public required string Name { get; set; }

    public string? Brand { get; set; }

    /// <summary>Key of the fixed category list (<c>ProductCategories</c>).</summary>
    public required string CategoryKey { get; set; }

    /// <summary>Unit code (<c>Unit.Code</c>) suggested for new batches and shopping entries.</summary>
    public required string DefaultUnitCode { get; set; }

    public Guid? DefaultStorageLocationId { get; set; }

    /// <summary>Store section overriding the category default; null means the category default.</summary>
    public string? SectionKey { get; set; }

    /// <summary>Minimum stock amount; set together with <see cref="MinimumStockUnitCode"/> or not at all.</summary>
    public decimal? MinimumStockValue { get; set; }

    public string? MinimumStockUnitCode { get; set; }

    /// <summary>True while a minimum-stock entry is on the shopping list, so it is not added twice.</summary>
    public bool MinimumStockTriggered { get; set; }

    /// <summary>Shelf life of the last added batch in days, offered as suggestion for the next one.</summary>
    public int? LastShelfLifeDays { get; set; }

    public ExpiryKind? LastExpiryKind { get; set; }

    /// <summary>Barcode whose Open Food Facts data the product was created from.</summary>
    public string? FoodFactsBarcode { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Also set when only <see cref="Barcodes"/> change: barcodes sync as part of the product.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }

    public ICollection<ProductBarcode> Barcodes { get; } = [];
}

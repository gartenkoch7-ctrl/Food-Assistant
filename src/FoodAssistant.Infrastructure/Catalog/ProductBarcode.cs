using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>
/// A barcode (EAN/GTIN) of a product; unique per household. Not sync-tracked on its own: whoever
/// changes barcodes also sets <see cref="Product.UpdatedAt"/>, so the product carries the change.
/// </summary>
public sealed class ProductBarcode : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Guid ProductId { get; init; }

    public required string Code { get; init; }
}

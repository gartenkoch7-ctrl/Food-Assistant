using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Restock;

/// <summary>Everything the minimum-stock check needs to know about one product.</summary>
/// <param name="Name">Product name, becomes the text of the list entry.</param>
/// <param name="CategoryKey">Category of the product (section fallback).</param>
/// <param name="SectionKey">Store section overriding the category default, or <see langword="null"/>.</param>
/// <param name="Minimum">Minimum stock, or <see langword="null"/> if none is set.</param>
/// <param name="Triggered">A minimum-stock entry was added and the stock was not sufficient again since.</param>
/// <param name="Batches">All batches of the product.</param>
/// <param name="OnOpenList">An unchecked entry for the product exists on some list of the household.</param>
public sealed record ProductStockState(
    string Name,
    string CategoryKey,
    string? SectionKey,
    Quantity? Minimum,
    bool Triggered,
    IReadOnlyList<StockBatch> Batches,
    bool OnOpenList);

/// <summary>Reads and marks the minimum-stock state of products of the active household.</summary>
public interface IRestockStore
{
    /// <summary>State of an active (not archived) product, or <see langword="null"/> if it does not exist.</summary>
    Task<ProductStockState?> GetProductStockAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the "triggered" memory of the product. Returns <see langword="true"/> only if this call
    /// changed it; <see langword="false"/> if it already had the value, the product is gone, or a
    /// concurrent change won. Callers use that as a claim so two parallel checks add one entry.
    /// </summary>
    Task<bool> SetTriggeredAsync(Guid productId, bool triggered, CancellationToken cancellationToken);
}

using FoodAssistant.Application.Activity;
using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Checkout;

/// <summary>The batch a bought entry becomes.</summary>
/// <param name="Batch">Domain batch (origin <c>Purchase</c>).</param>
/// <param name="ProductId">Its product; the id of <paramref name="NewProduct"/> for a new one.</param>
/// <param name="NewProduct">Product to create first, or null for an existing one.</param>
/// <param name="StorageLocationId">Location of the batch.</param>
/// <param name="Remember">Shelf life to remember on the product for the next purchase, or null to keep it.</param>
/// <param name="Activity">Feed entry with undo data, stored only if the line is applied.</param>
public sealed record CheckoutBatch(
    StockBatch Batch,
    Guid ProductId,
    CheckoutNewProduct? NewProduct,
    Guid StorageLocationId,
    LastPurchase? Remember,
    ActivityEntry Activity);

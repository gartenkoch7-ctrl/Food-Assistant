using FoodAssistant.Domain.Checkout;

namespace FoodAssistant.Application.Checkout;

/// <summary>A product (not archived) with what the checkout suggestions need.</summary>
/// <param name="Id">Product id.</param>
/// <param name="CategoryKey">Category key.</param>
/// <param name="DefaultUnitCode">Default unit code.</param>
/// <param name="DefaultStorageLocationId">Default location, if any.</param>
/// <param name="LastBatchLocationId">Location of the most recently added existing batch, if any.</param>
/// <param name="LastPurchase">Shelf life of the last purchase, if remembered.</param>
public sealed record CheckoutProduct(
    Guid Id,
    string CategoryKey,
    string DefaultUnitCode,
    Guid? DefaultStorageLocationId,
    Guid? LastBatchLocationId,
    LastPurchase? LastPurchase);

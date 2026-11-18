using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Domain.Checkout;

/// <summary>
/// Shelf life of the last bought batch of a product ("letzter Kauf"): days from the purchase to
/// the printed date, and the kind of that date. Suggested again for the next purchase.
/// </summary>
public sealed record LastPurchase(int Days, ExpiryKind Kind);

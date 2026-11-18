using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.Checkout;

/// <summary>
/// A bought (checked) entry with suggested values for its batch. <paramref name="ProductId"/> is
/// null for free text: the person assigns a product or skips the entry.
/// </summary>
/// <param name="ItemId">Shopping list entry.</param>
/// <param name="ItemVersion">Version of the entry; sent back so a changed entry is not booked.</param>
/// <param name="ProductId">Linked product, or null for free text (or an archived product).</param>
/// <param name="Text">Text of the entry (product name or free text).</param>
/// <param name="CategoryKey">Category of the product; null for free text.</param>
/// <param name="Quantity">Amount of the entry, else 1 × default unit of the product, else 1 piece.</param>
/// <param name="StorageLocationId">Suggested location, or null if the household has none.</param>
/// <param name="ExpiresOn">Suggested date, or null.</param>
/// <param name="ExpiryKind">Kind of <paramref name="ExpiresOn"/>; <c>none</c> exactly without date.</param>
/// <param name="ExpiryFromRule">
/// The date comes from the shelf-life rule "frozen" (frozen at home, a use-by date). The client
/// then sends no package date; the server computes the date again.
/// </param>
/// <param name="QuantityFromEntry">
/// <paramref name="Quantity"/> is the amount of the list entry, not a default; a product assigned to
/// free text then keeps it instead of taking its own default unit.
/// </param>
public sealed record CheckoutEntryDto(
    Guid ItemId,
    long ItemVersion,
    Guid? ProductId,
    string Text,
    string? CategoryKey,
    QuantityDto Quantity,
    Guid? StorageLocationId,
    DateOnly? ExpiresOn,
    ExpiryKindDto ExpiryKind,
    bool ExpiryFromRule,
    bool QuantityFromEntry);

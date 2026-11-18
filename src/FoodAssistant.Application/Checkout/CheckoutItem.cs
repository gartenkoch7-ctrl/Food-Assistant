using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Checkout;

/// <summary>A checked, not deleted entry of the list being completed.</summary>
/// <param name="Id">Shopping list entry.</param>
/// <param name="Version">Its version (<c>ShoppingItems.Version</c>).</param>
/// <param name="ProductId">Linked product, or null for free text.</param>
/// <param name="Text">Product name or free text (shown only, never logged).</param>
/// <param name="Quantity">Amount of the entry, if any.</param>
public sealed record CheckoutItem(Guid Id, long Version, Guid? ProductId, string Text, Quantity? Quantity);

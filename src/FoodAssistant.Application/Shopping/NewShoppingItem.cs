using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>A validated entry to add; text and section are already resolved (product name, section key).</summary>
/// <param name="Id">UUIDv7 of the entry (from the client or <c>Guid.CreateVersion7()</c>).</param>
/// <param name="ProductId">Linked product, or <see langword="null"/> for free text.</param>
/// <param name="Text">Product name or free text, 1–200 characters.</param>
/// <param name="Quantity">Optional amount.</param>
/// <param name="Note">Optional note, at most 500 characters.</param>
/// <param name="SectionKey">One of <see cref="StoreSections.All"/>.</param>
/// <param name="Origin">How the entry came onto the list.</param>
/// <param name="CreatedByUserId">Person who added it; <see langword="null"/> or the system user for background jobs.</param>
public sealed record NewShoppingItem(
    Guid Id,
    Guid? ProductId,
    string Text,
    Quantity? Quantity,
    string? Note,
    string SectionKey,
    ShoppingItemOrigin Origin,
    Guid? CreatedByUserId);

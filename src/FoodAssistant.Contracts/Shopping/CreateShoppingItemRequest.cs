using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Shopping;

/// <summary>Adds an entry to a list: a product or free text.</summary>
/// <param name="Id">
/// Client-generated UUIDv7, or <see langword="null"/>. Sending a known id again returns the
/// existing entry (200) instead of creating a second one, so the offline queue can retry safely.
/// </param>
/// <param name="ProductId">Product of the household; text and section then come from the product.</param>
/// <param name="Text">Free text (1–200 characters); ignored when <paramref name="ProductId"/> is set.</param>
/// <param name="Quantity">Optional amount (value &gt; 0).</param>
/// <param name="Note">Optional note (at most 500 characters).</param>
/// <param name="SectionKey">Section of a free-text entry; <c>other</c> when omitted. Ignored for products.</param>
public sealed record CreateShoppingItemRequest(
    Guid? Id,
    Guid? ProductId,
    string? Text,
    QuantityDto? Quantity,
    string? Note,
    string? SectionKey);

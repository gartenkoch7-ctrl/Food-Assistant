using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Shopping;

/// <summary>An entry on a shopping list.</summary>
/// <param name="Id">Item id (UUIDv7, usually created by the client).</param>
/// <param name="ListId">List the entry is on.</param>
/// <param name="ProductId">Linked product, or <see langword="null"/> for free text.</param>
/// <param name="Text">Product name or free text.</param>
/// <param name="Quantity">Optional amount.</param>
/// <param name="Note">Optional note.</param>
/// <param name="SectionKey">Store section key (see <c>GET /shopping/sections</c>).</param>
/// <param name="Origin">How the entry came onto the list.</param>
/// <param name="Checked">Bought ("abgehakt").</param>
/// <param name="CheckedAt">When it was checked, if it is.</param>
/// <param name="Version">
/// Version of the entry. Changes send it as <c>baseVersion</c> instead of <c>If-Match</c>, because
/// offline changes are merged by the conflict rules of ADR-0005 rather than refused.
/// </param>
/// <param name="UpdatedAt">When the server stored the last change.</param>
public sealed record ShoppingItemDto(
    Guid Id,
    Guid ListId,
    Guid? ProductId,
    string Text,
    QuantityDto? Quantity,
    string? Note,
    string SectionKey,
    ShoppingItemOriginDto Origin,
    bool Checked,
    DateTimeOffset? CheckedAt,
    long Version,
    DateTimeOffset UpdatedAt);

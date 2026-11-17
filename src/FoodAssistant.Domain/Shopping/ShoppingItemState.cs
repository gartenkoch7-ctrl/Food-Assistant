using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Shopping;

/// <summary>Snapshot of a shopping list item as used by the conflict rules of ADR-0005.</summary>
/// <param name="Id">Item id (UUIDv7, created by the client for new items).</param>
/// <param name="Version">
/// Server version. For an incoming change it is the last version the client knew; 0 for an item the
/// server has never stored.
/// </param>
/// <param name="Checked">Bought ("abgehakt").</param>
/// <param name="Quantity">Optional amount.</param>
/// <param name="Note">Optional free text.</param>
/// <param name="Deleted">Removed from the list (tombstone).</param>
/// <param name="UpdatedAt">
/// Time the server received the change; for an incoming change the time of its arrival. Supplied by
/// the caller (TimeProvider), never read from a clock here.
/// </param>
public sealed record ShoppingItemState(
    Guid Id,
    long Version,
    bool Checked,
    Quantity? Quantity,
    string? Note,
    bool Deleted,
    DateTimeOffset UpdatedAt);

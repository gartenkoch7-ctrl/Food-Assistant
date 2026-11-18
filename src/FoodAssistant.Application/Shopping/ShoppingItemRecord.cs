using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>All stored fields of a shopping list entry, including tombstones (<see cref="DeletedAt"/>).</summary>
public sealed record ShoppingItemRecord(
    Guid Id,
    Guid ListId,
    Guid? ProductId,
    string Text,
    Quantity? Quantity,
    string? Note,
    string SectionKey,
    ShoppingItemOrigin Origin,
    bool Checked,
    DateTimeOffset? CheckedAt,
    Guid? CheckedByUserId,
    long Version,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt)
{
    /// <summary>Removed from the list; kept as tombstone for sync.</summary>
    public bool Deleted => DeletedAt is not null;

    /// <summary>Snapshot for <see cref="ShoppingConflictResolver"/>.</summary>
    public ShoppingItemState ToState() => new(Id, Version, Checked, Quantity, Note, Deleted, UpdatedAt);
}

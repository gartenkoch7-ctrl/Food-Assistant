using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>A shopping list; every household has exactly one default list.</summary>
public sealed class ShoppingList : ISyncTracked
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    /// <summary>Display name; empty for the automatically created default list (the UI shows a translated name).</summary>
    public required string Name { get; set; }

    /// <summary>Store whose section order the list uses.</summary>
    public Guid? StoreId { get; set; }

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>A storage location of a household (fridge, freezer, pantry); its type selects the shelf-life rules.</summary>
public sealed class StorageLocation : ISyncTracked
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public required string Name { get; set; }

    public StorageType Type { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.ShelfLife;

/// <summary>
/// Household-specific shelf-life values for either a category or a product (exactly one of both)
/// and one storage type (ADR-0010). Missing values fall back to the next less specific rule.
/// </summary>
public sealed class ShelfLifeOverride : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    /// <summary>Category key; set exactly when <see cref="ProductId"/> is not.</summary>
    public string? CategoryKey { get; init; }

    public Guid? ProductId { get; init; }

    public StorageType StorageType { get; init; }

    public int? DaysAfterOpening { get; set; }

    public int? DaysFrozen { get; set; }

    public int? DaysAfterThawing { get; set; }

    public int? DaysAsLeftover { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

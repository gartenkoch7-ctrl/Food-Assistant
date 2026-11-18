using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Stores;

/// <summary>Manually set position of a section in a store; overrides the learned order.</summary>
public sealed class StoreSectionOrder : IHouseholdOwned
{
    public Guid HouseholdId { get; init; }

    public Guid StoreId { get; init; }

    /// <summary>Store section key (<c>StoreSections</c>).</summary>
    public required string SectionKey { get; init; }

    public int Position { get; set; }
}

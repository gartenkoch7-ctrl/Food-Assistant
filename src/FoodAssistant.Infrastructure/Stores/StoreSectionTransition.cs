using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Stores;

/// <summary>How often shoppers went from one section to another in a store; source of the learned order.</summary>
public sealed class StoreSectionTransition : IHouseholdOwned
{
    public Guid HouseholdId { get; init; }

    public Guid StoreId { get; init; }

    public required string FromSectionKey { get; init; }

    public required string ToSectionKey { get; init; }

    public int Count { get; set; }
}

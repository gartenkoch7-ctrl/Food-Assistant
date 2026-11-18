namespace FoodAssistant.Contracts.Sync;

/// <summary>Changes of one household, pushed via <see cref="HouseholdHubContract.Changed"/>.</summary>
public sealed record HouseholdChangesMessage(Guid HouseholdId, IReadOnlyList<HouseholdChangeDto> Changes);

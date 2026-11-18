using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>A person's membership in a household.</summary>
public sealed record HouseholdMembershipInfo(Guid HouseholdId, Guid UserId, HouseholdRole Role);

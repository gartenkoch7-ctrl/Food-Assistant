using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Contracts.Accounts;

/// <summary>Result of accepting an invitation.</summary>
public sealed record AcceptInvitationResponse(Guid UserId, Guid HouseholdId, HouseholdRole Role);

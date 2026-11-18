namespace FoodAssistant.Contracts.Households;

/// <summary>Body of <c>POST /api/v1/households/{householdId}/invitations</c>.</summary>
public sealed record CreateInvitationRequest(HouseholdRole Role);

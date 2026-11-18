namespace FoodAssistant.Contracts.Households;

/// <summary>Body of <c>PUT /api/v1/households/{householdId}/members/{userId}/role</c>.</summary>
public sealed record ChangeMemberRoleRequest(HouseholdRole Role);

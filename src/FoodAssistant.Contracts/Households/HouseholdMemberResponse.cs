namespace FoodAssistant.Contracts.Households;

/// <summary>A member of a household.</summary>
public sealed record HouseholdMemberResponse(Guid UserId, string DisplayName, HouseholdRole Role, DateTimeOffset JoinedAt);

namespace FoodAssistant.Contracts.Households;

/// <summary>A household as returned after creation.</summary>
public sealed record HouseholdResponse(Guid Id, string Name, string TimeZone);

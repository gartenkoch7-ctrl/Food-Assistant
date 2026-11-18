using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Contracts.Accounts;

/// <summary>A household of the signed-in person with their role in it.</summary>
public sealed record MeHouseholdResponse(Guid Id, string Name, string TimeZone, HouseholdRole Role);

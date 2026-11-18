using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Either the new household or validation errors (field → stable error codes).</summary>
public sealed record CreateHouseholdResult(HouseholdResponse? Household, IReadOnlyDictionary<string, string[]> Errors);

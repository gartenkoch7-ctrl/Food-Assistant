namespace FoodAssistant.Contracts.Households;

/// <summary>Body of <c>PUT /api/v1/households/{householdId}</c>; <paramref name="TimeZone"/> is an IANA id.</summary>
public sealed record UpdateHouseholdRequest(string Name, string TimeZone, int ExpiryLeadDays);

namespace FoodAssistant.Contracts.Households;

/// <summary>Body of <c>POST /api/v1/households</c>. <paramref name="TimeZone"/> is an IANA id, e.g. <c>Europe/Berlin</c>.</summary>
public sealed record CreateHouseholdRequest(string Name, string TimeZone);

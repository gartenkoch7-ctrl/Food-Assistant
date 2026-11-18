namespace FoodAssistant.Contracts.Households;

/// <summary>
/// Settings of a household. <paramref name="Version"/> is the ETag value; changes send it back in
/// <c>If-Match</c> (ADR-0004). <paramref name="ExpiryLeadDays"/> is the lead time of "expires soon".
/// </summary>
public sealed record HouseholdSettingsDto(Guid Id, string Name, string TimeZone, int ExpiryLeadDays, long Version);

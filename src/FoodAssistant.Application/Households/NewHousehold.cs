namespace FoodAssistant.Application.Households;

/// <summary>Validated data for a new household. <paramref name="TimeZoneId"/> is an IANA id.</summary>
public sealed record NewHousehold(string Name, string TimeZoneId);

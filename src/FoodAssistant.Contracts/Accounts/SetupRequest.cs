namespace FoodAssistant.Contracts.Accounts;

/// <summary>
/// Body of <c>POST /api/v1/setup</c>: creates the server admin, the first household and makes the
/// admin its owner (ADR-0002). <paramref name="TimeZone"/> is an IANA id, e.g. <c>Europe/Berlin</c>.
/// </summary>
public sealed record SetupRequest(
    string UserName,
    string Password,
    string DisplayName,
    string HouseholdName,
    string TimeZone);

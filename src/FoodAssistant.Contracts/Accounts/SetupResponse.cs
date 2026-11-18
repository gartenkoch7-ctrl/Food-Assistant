namespace FoodAssistant.Contracts.Accounts;

/// <summary>Result of the setup: the new admin account and the first household.</summary>
public sealed record SetupResponse(Guid UserId, Guid HouseholdId);

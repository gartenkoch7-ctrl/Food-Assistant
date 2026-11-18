namespace FoodAssistant.Contracts.Accounts;

/// <summary>Answer of <c>GET /api/v1/setup</c>: once an account exists, setup is closed for good.</summary>
public sealed record SetupStatusResponse(bool IsSetupComplete);

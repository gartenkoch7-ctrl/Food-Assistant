namespace FoodAssistant.Application.Accounts;

/// <summary>Data for a new account. Password rules are enforced by the identity system (ADR-0003).</summary>
public sealed record NewUserAccount(string UserName, string Password, string DisplayName);

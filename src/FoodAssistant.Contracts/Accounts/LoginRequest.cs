namespace FoodAssistant.Contracts.Accounts;

/// <summary>Body of <c>POST /api/v1/auth/login</c> (web cookie sign-in).</summary>
public sealed record LoginRequest(string UserName, string Password);

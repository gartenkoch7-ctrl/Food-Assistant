namespace FoodAssistant.Application.Accounts;

/// <summary>A person's account as the application sees it (no password data).</summary>
/// <param name="SecurityStamp">
/// Changes when credentials change; issued app sessions compare it so a password change ends them.
/// Never returned by the API.
/// </param>
public sealed record UserAccount(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsServerAdmin,
    bool MustChangePassword,
    string SecurityStamp);

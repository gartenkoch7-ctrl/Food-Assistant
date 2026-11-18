namespace FoodAssistant.Contracts.Accounts;

/// <summary>
/// Body of <c>POST /api/v1/invitations/accept</c>. Anonymous callers create a new account and must
/// send <paramref name="UserName"/>, <paramref name="Password"/> and <paramref name="DisplayName"/>;
/// signed-in callers join the household with their existing account and send only the token.
/// </summary>
public sealed record AcceptInvitationRequest(
    string Token,
    string? UserName = null,
    string? Password = null,
    string? DisplayName = null);

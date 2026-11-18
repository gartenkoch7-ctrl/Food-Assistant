namespace FoodAssistant.Contracts.Accounts;

/// <summary>
/// Answer of <c>GET /api/v1/auth/antiforgery</c>. Cookie-authenticated writes send
/// <paramref name="RequestToken"/> in the header named <paramref name="HeaderName"/>.
/// The token is bound to the signed-in person: fetch a new one after login.
/// </summary>
public sealed record AntiforgeryTokenResponse(string RequestToken, string HeaderName);

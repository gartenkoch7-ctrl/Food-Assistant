namespace FoodAssistant.Server.Auth;

/// <summary>Authentication scheme names of the server.</summary>
public static class AuthSchemes
{
    /// <summary>
    /// Default scheme: requests with <c>Authorization: Bearer</c> are validated as OpenIddict access
    /// tokens (app), all others use the Identity cookie (web, ADR-0003).
    /// </summary>
    public const string CookieOrBearer = "CookieOrBearer";
}

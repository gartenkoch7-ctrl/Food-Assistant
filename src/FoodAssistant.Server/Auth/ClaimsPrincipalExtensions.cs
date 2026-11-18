using System.Security.Claims;
using OpenIddict.Abstractions;

namespace FoodAssistant.Server.Auth;

/// <summary>Reads the account id; cookies and access tokens both carry it as <c>sub</c>.</summary>
public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;
        return principal?.Identity?.IsAuthenticated == true
               && Guid.TryParse(principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value, out userId);
    }

    /// <summary>The account id of an authenticated request (guaranteed by the endpoint's policy).</summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal) =>
        principal.TryGetUserId(out var userId)
            ? userId
            : throw new InvalidOperationException("The request has no authenticated account.");
}

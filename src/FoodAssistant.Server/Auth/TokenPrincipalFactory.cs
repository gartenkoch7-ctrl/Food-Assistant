using System.Collections.Immutable;
using System.Security.Claims;
using FoodAssistant.Application.Accounts;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Builds the principal OpenIddict turns into codes and tokens. Access tokens carry only the account
/// id (<c>sub</c>); names go into the identity token and only with the <c>profile</c> scope.
/// Rights are never put into tokens – policies check the database on every request.
/// </summary>
internal static class TokenPrincipalFactory
{
    /// <summary>
    /// Private claim without destination: it stays inside authorization codes and refresh tokens
    /// (which only the server reads) and never reaches access or identity tokens.
    /// </summary>
    private const string SecurityStampClaim = "fa_security_stamp";

    public static ClaimsPrincipal Create(UserAccount account, ImmutableArray<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(account);

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, account.Id.ToString())
            .SetClaim(Claims.Name, account.DisplayName)
            .SetClaim(Claims.PreferredUsername, account.UserName)
            .SetClaim(SecurityStampClaim, account.SecurityStamp);
        identity.SetScopes(scopes);

        var withProfile = identity.HasScope(Scopes.Profile);
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name or Claims.PreferredUsername when withProfile => [Destinations.IdentityToken],
            _ => [],
        });

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Whether a code or refresh token was issued under the account's current security stamp; a
    /// password change (new stamp) thereby ends all app sessions at their next refresh.
    /// </summary>
    public static bool HasCurrentSecurityStamp(ClaimsPrincipal stored, UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(account);
        var issuedWith = stored.GetClaim(SecurityStampClaim);
        return !string.IsNullOrEmpty(issuedWith) && string.Equals(issuedWith, account.SecurityStamp, StringComparison.Ordinal);
    }
}

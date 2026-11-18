using FoodAssistant.Application.Accounts;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Contracts.Accounts;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Pass-through handlers of the OpenID Connect endpoints (ADR-0003). OpenIddict validates the
/// protocol (client, redirect URI, PKCE, scopes) before these run; they only decide who signs in.
/// </summary>
internal static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).ExcludeFromDescription();
        endpoints.MapPost("/connect/token", ExchangeAsync).ExcludeFromDescription();
        endpoints.MapMethods("/connect/endsession", [HttpMethods.Get, HttpMethods.Post], (Delegate)EndSessionAsync).ExcludeFromDescription();
        return endpoints;
    }

    /// <summary>
    /// Uses the web sign-in cookie. Without it: 401 – the login page arrives with the UI (M1); until
    /// then a client signs in via <c>POST /api/v1/auth/login</c> first.
    /// </summary>
    private static async Task<IResult> AuthorizeAsync(
        HttpContext httpContext,
        IUserAccountStore accounts,
        ILoggerFactory loggerFactory)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.authorize");
        var logger = loggerFactory.CreateLogger(AuthLog.Category);
        var request = httpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var cookie = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        if (!cookie.Succeeded || !cookie.Principal.TryGetUserId(out var userId))
        {
            AuthLog.AuthorizeRequiresLogin(logger);
            return ProblemResults.Problem(
                StatusCodes.Status401Unauthorized,
                AccountErrorCodes.LoginRequired,
                "Sign-in required.",
                "Sign in via POST /api/v1/auth/login, then repeat this request. A login page follows with the web UI.");
        }

        var account = await accounts.FindByIdAsync(userId, httpContext.RequestAborted).ConfigureAwait(false);
        if (account is null)
        {
            return Reject(Errors.LoginRequired, "The account is not allowed to sign in.");
        }

        AuthLog.AuthorizationCodeIssued(logger, request.ClientId, account.Id);
        return Results.SignIn(
            TokenPrincipalFactory.Create(account, request.GetScopes()),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Authorization code and refresh token grants; the account is re-checked each time.</summary>
    private static async Task<IResult> ExchangeAsync(
        HttpContext httpContext,
        IUserAccountStore accounts,
        ILoggerFactory loggerFactory)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.token");
        var logger = loggerFactory.CreateLogger(AuthLog.Category);
        var request = httpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        activity?.SetTag("foodassistant.grant_type", request.GrantType);

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Reject(Errors.UnsupportedGrantType, "The grant type is not supported.");
        }

        var stored = await httpContext
            .AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
        var account = stored.Principal.TryGetUserId(out var userId)
            ? await accounts.FindByIdAsync(userId, httpContext.RequestAborted).ConfigureAwait(false)
            : null;
        if (account is null || !TokenPrincipalFactory.HasCurrentSecurityStamp(stored.Principal!, account))
        {
            AuthLog.TokenRefused(logger, request.GrantType);
            return Reject(Errors.InvalidGrant, "The account was deleted or its credentials changed; sign in again.");
        }

        AuthLog.TokensIssued(logger, request.GrantType, account.Id);
        return Results.SignIn(
            TokenPrincipalFactory.Create(account, stored.Principal!.GetScopes()),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Ends the browser session only for a request carrying a valid <c>id_token_hint</c> of the same
    /// person: a plain cross-site link must not be able to sign anyone out (logout CSRF). App tokens
    /// are ended through <c>/connect/revoke</c>.
    /// </summary>
    private static async Task<IResult> EndSessionAsync(HttpContext httpContext)
    {
        var hint = await httpContext
            .AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
        var cookie = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        if (hint.Principal.TryGetUserId(out var hintUserId)
            && cookie.Principal.TryGetUserId(out var cookieUserId)
            && hintUserId == cookieUserId)
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        }

        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IResult Reject(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}

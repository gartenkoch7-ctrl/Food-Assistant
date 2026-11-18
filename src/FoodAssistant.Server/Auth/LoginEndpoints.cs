using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Infrastructure.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// <c>/api/v1/auth</c>: web sign-in with the HttpOnly session cookie (ADR-0003, BFF). The app uses
/// <c>/connect/*</c> instead.
/// </summary>
internal static class LoginEndpoints
{
    public static RouteGroupBuilder MapLoginEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapGet("/antiforgery", GetAntiforgeryToken).AllowAnonymous();
        auth.MapPost("/login", LoginAsync).AllowAnonymous().AddEndpointFilter(new CookieAntiforgeryFilter(always: true));
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization().AddEndpointFilter(new CookieAntiforgeryFilter(always: true));
        return api;
    }

    private static Ok<AntiforgeryTokenResponse> GetAntiforgeryToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!, CookieAntiforgeryFilter.HeaderName));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<User> signInManager,
        ILoggerFactory loggerFactory)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.login");
        var logger = loggerFactory.CreateLogger(AuthLog.Category);
        var users = signInManager.UserManager;

        var user = string.IsNullOrWhiteSpace(request.UserName)
            ? null
            : await users.FindByNameAsync(request.UserName.Trim()).ConfigureAwait(false);
        if (user is null)
        {
            // Spend the same hashing time as for a real account so response times do not reveal
            // which user names exist.
            _ = users.PasswordHasher.HashPassword(new User(), request.Password ?? string.Empty);
            AuthLog.LoginFailedUnknownUser(logger);
            activity?.SetTag("foodassistant.outcome", "invalid_credentials");
            return InvalidCredentials();
        }

        var result = await signInManager
            .PasswordSignInAsync(user, request.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            AuthLog.LoginSucceeded(logger, user.Id);
            activity?.SetTag("foodassistant.outcome", "succeeded");
            return TypedResults.NoContent();
        }

        if (result.IsLockedOut)
        {
            AuthLog.LoginLockedOut(logger, user.Id);
            activity?.SetTag("foodassistant.outcome", "locked_out");
            return ProblemResults.Problem(
                StatusCodes.Status429TooManyRequests,
                AccountErrorCodes.LockedOut,
                "Too many failed attempts. Try again in 15 minutes.");
        }

        AuthLog.LoginFailed(logger, user.Id);
        activity?.SetTag("foodassistant.outcome", "invalid_credentials");
        return InvalidCredentials();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        SignInManager<User> signInManager,
        ILoggerFactory loggerFactory)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.logout");
        var logger = loggerFactory.CreateLogger(AuthLog.Category);
        await signInManager.SignOutAsync().ConfigureAwait(false);
        if (httpContext.User.TryGetUserId(out var userId))
        {
            AuthLog.LoggedOut(logger, userId);
        }

        return TypedResults.NoContent();
    }

    private static IResult InvalidCredentials() =>
        ProblemResults.Problem(
            StatusCodes.Status401Unauthorized,
            AccountErrorCodes.InvalidCredentials,
            "User name or password is wrong.");
}

using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Log events of sign-in, tokens and access checks (Server range 1100–1199). Only account and
/// household ids – never user names, display names, passwords or tokens (AGENTS.md 2.5).
/// </summary>
internal static partial class AuthLog
{
    /// <summary>Logger category for the static endpoint classes of this folder.</summary>
    public const string Category = "FoodAssistant.Server.Auth";

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "User {UserId} signed in")]
    public static partial void LoginSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Sign-in failed: unknown user name")]
    public static partial void LoginFailedUnknownUser(ILogger logger);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Sign-in failed for user {UserId}: wrong password")]
    public static partial void LoginFailed(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Sign-in refused: user {UserId} is locked out")]
    public static partial void LoginLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "User {UserId} signed out")]
    public static partial void LoggedOut(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Warning, Message = "Cookie request without valid antiforgery token rejected")]
    public static partial void AntiforgeryRejected(ILogger logger);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Information, Message = "Authorization request without web sign-in rejected")]
    public static partial void AuthorizeRequiresLogin(ILogger logger);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Information,
        Message = "Authorization code issued to client {ClientId} for user {UserId}")]
    public static partial void AuthorizationCodeIssued(ILogger logger, string? clientId, Guid userId);

    [LoggerMessage(EventId = 1108, Level = LogLevel.Information, Message = "Tokens issued ({GrantType}) for user {UserId}")]
    public static partial void TokensIssued(ILogger logger, string? grantType, Guid userId);

    [LoggerMessage(EventId = 1109, Level = LogLevel.Warning,
        Message = "Token request ({GrantType}) refused: account missing or locked out")]
    public static partial void TokenRefused(ILogger logger, string? grantType);

    [LoggerMessage(EventId = 1110, Level = LogLevel.Information, Message = "OpenID client {ClientId} registered")]
    public static partial void ClientRegistered(ILogger logger, string clientId);

    [LoggerMessage(EventId = 1111, Level = LogLevel.Information, Message = "New token {KeyPurpose} key created")]
    public static partial void TokenKeyCreated(ILogger logger, string keyPurpose);

    [LoggerMessage(EventId = 1112, Level = LogLevel.Information,
        Message = "Household access denied: not a member of the requested household")]
    public static partial void HouseholdNotAccessible(ILogger logger);

    [LoggerMessage(EventId = 1113, Level = LogLevel.Information,
        Message = "Household access denied: role {Role} in household {HouseholdId} is not sufficient")]
    public static partial void HouseholdRoleInsufficient(ILogger logger, Guid householdId, HouseholdRole role);

    [LoggerMessage(EventId = 1114, Level = LogLevel.Warning, Message = "Server admin right required, denied for user {UserId}")]
    public static partial void ServerAdminDenied(ILogger logger, Guid userId);
}

using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Accounts;

/// <summary>
/// Log events of the accounts feature (Application range 3000–3099). Only ids and outcomes –
/// never user names, display names, passwords or tokens.
/// </summary>
internal static partial class AccountsLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "Setup completed: admin {UserId}, first household {HouseholdId}")]
    public static partial void SetupCompleted(ILogger logger, Guid userId, Guid householdId);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "Setup refused: it was already completed")]
    public static partial void SetupRejected(ILogger logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Invitation {InvitationId} accepted: user {UserId} joined household {HouseholdId} as {Role}")]
    public static partial void InvitationAccepted(
        ILogger logger,
        Guid invitationId,
        Guid householdId,
        Guid userId,
        HouseholdRole role);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Invitation not accepted: {Reason}")]
    public static partial void InvitationRejected(ILogger logger, ProvisioningStatus reason);
}

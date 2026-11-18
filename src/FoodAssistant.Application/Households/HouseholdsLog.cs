using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Households;

/// <summary>Log events of the households feature (Application range 3000–3099). Only ids, no names.</summary>
internal static partial class HouseholdsLog
{
    [LoggerMessage(EventId = 3020, Level = LogLevel.Information, Message = "Household {HouseholdId} created")]
    public static partial void HouseholdCreated(ILogger logger, Guid householdId);

    [LoggerMessage(EventId = 3021, Level = LogLevel.Information,
        Message = "Invitation {InvitationId} created for household {HouseholdId} with role {Role}")]
    public static partial void InvitationCreated(ILogger logger, Guid invitationId, Guid householdId, HouseholdRole role);

    [LoggerMessage(EventId = 3022, Level = LogLevel.Information,
        Message = "Invitation {InvitationId} of household {HouseholdId} revoked")]
    public static partial void InvitationRevoked(ILogger logger, Guid invitationId, Guid householdId);

    [LoggerMessage(EventId = 3023, Level = LogLevel.Information,
        Message = "Role of user {UserId} in household {HouseholdId} changed to {Role}")]
    public static partial void MemberRoleChanged(ILogger logger, Guid householdId, Guid userId, HouseholdRole role);

    [LoggerMessage(EventId = 3024, Level = LogLevel.Information,
        Message = "User {UserId} removed from household {HouseholdId}")]
    public static partial void MemberRemoved(ILogger logger, Guid householdId, Guid userId);

    [LoggerMessage(EventId = 3025, Level = LogLevel.Warning,
        Message = "Change refused: it would leave household {HouseholdId} without an owner")]
    public static partial void LastOwnerProtected(ILogger logger, Guid householdId);

    [LoggerMessage(EventId = 3026, Level = LogLevel.Information, Message = "Settings of household {HouseholdId} changed")]
    public static partial void SettingsChanged(ILogger logger, Guid householdId);
}

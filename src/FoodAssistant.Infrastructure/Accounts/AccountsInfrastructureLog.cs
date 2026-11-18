using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Accounts;

/// <summary>Log events of account persistence (Infrastructure range 2000–2199, block 2020–2039).</summary>
internal static partial class AccountsInfrastructureLog
{
    /// <param name="errorCodes">Identity error codes such as <c>PasswordTooShort</c> – never input values.</param>
    [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "Account not created: {ErrorCodes}")]
    public static partial void AccountCreationRefused(ILogger logger, string errorCodes);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Warning,
        Message = "Invitation {InvitationId} was redeemed concurrently; this redemption was rolled back")]
    public static partial void InvitationRedemptionConflict(ILogger logger, Guid invitationId);
}

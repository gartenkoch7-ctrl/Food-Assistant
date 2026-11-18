using Microsoft.Extensions.Logging;

namespace FoodAssistant.Server.Sync;

/// <summary>Log events of live updates (Server Sync range 1300–1324). Only ids.</summary>
internal static partial class SyncLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information,
        Message = "Joining a household group was refused: not a member")]
    public static partial void HubJoinDenied(ILogger logger);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Debug, Message = "Connection joined live updates of household {HouseholdId}")]
    public static partial void HubJoined(ILogger logger, Guid householdId);
}

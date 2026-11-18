using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>Log events of the sync seam (Infrastructure Sync range 2200–2249).</summary>
internal static partial class SyncLog
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Warning,
        Message = "Live update for household {HouseholdId} could not be sent; clients catch up through the change log")]
    public static partial void ChangeNotificationFailed(ILogger logger, Guid householdId, Exception exception);
}

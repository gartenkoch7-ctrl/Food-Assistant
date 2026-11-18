using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Sync;

/// <summary>Log events of the live-update client (4100-4149 are shared with the UI project). No user content.</summary>
internal static partial class SyncLog
{
    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Live connection lost, reconnecting")]
    public static partial void LiveConnectionLost(ILogger logger);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information, Message = "Live connection restored")]
    public static partial void LiveReconnected(ILogger logger);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning, Message = "Joining live updates failed: {ErrorCode}")]
    public static partial void LiveJoinFailed(ILogger logger, string errorCode);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Debug, Message = "Live connection attempt failed")]
    public static partial void LiveConnectFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Warning, Message = "A handler of a live update failed")]
    public static partial void LiveHandlerFailed(ILogger logger, Exception exception);
}

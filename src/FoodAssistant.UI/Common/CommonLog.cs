using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Common;

/// <summary>Log messages of the shared UI code. EventIds 4000-4099 are reserved for the UI project.</summary>
internal static partial class CommonLog
{
    // Only the operation name is logged, never stored values (AGENTS.md 2.5).
    [LoggerMessage(EventId = 4000, Level = LogLevel.Debug, Message = "Browser call {Operation} is unavailable")]
    public static partial void BrowserCallUnavailable(ILogger logger, Exception exception, string operation);
}

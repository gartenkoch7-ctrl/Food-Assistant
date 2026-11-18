using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Session;

/// <summary>Log messages of the session. EventIds 4200-4249 are reserved for UI/Client sessions and accounts.</summary>
internal static partial class SessionLog
{
    // Only the exception; the session holds names and households, which never go into logs (AGENTS.md 2.5).
    [LoggerMessage(EventId = 4200, Level = LogLevel.Warning, Message = "Reloading the session after an expired-session answer failed")]
    public static partial void ExpiryReloadFailed(ILogger logger, Exception exception);
}

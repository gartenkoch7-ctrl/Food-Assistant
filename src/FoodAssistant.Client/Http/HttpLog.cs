using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Http;

/// <summary>Log messages of the HTTP client base. EventIds 4000-4099 are shared with the UI project (4010 onwards here).</summary>
internal static partial class HttpLog
{
    // Method only: paths and queries can contain user input (AGENTS.md 2.5).
    [LoggerMessage(EventId = 4010, Level = LogLevel.Debug, Message = "API call ({Method}) failed before an answer arrived")]
    public static partial void NetworkError(ILogger logger, Exception exception, string method);

    // No exception: its message may quote parts of the body.
    [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "API answer ({Method}) reported success but its body could not be read")]
    public static partial void InvalidResponse(ILogger logger, string method);
}

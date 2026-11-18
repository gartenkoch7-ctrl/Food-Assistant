using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Accounts;

/// <summary>Log messages of the account pages. EventIds 4200-4249 are shared with the session (4200 is taken).</summary>
internal static partial class AccountsLog
{
    // Only the exception: never the link or the token it contains (AGENTS.md 2.5).
    [LoggerMessage(EventId = 4210, Level = LogLevel.Debug, Message = "Copying the invitation link to the clipboard failed")]
    public static partial void ClipboardUnavailable(ILogger logger, Exception exception);
}

using Microsoft.Extensions.Logging;

namespace FoodAssistant.UI.Scan;

/// <summary>Log messages of the scan page. EventIds 4400-4449.</summary>
internal static partial class ScanLog
{
    // Only the kind of failure is logged (a JS error name such as NotAllowedError), never scanned codes (AGENTS.md 2.5).
    [LoggerMessage(EventId = 4400, Level = LogLevel.Information, Message = "Camera scanning is unavailable: {Reason}")]
    public static partial void CameraUnavailable(ILogger logger, string reason);
}

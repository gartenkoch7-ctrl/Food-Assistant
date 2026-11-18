using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.FoodFacts;

/// <summary>
/// Log events of the food facts lookup (Application range 3350–3399). The barcode is never
/// logged: together with timestamps it would reveal what a household buys.
/// </summary>
internal static partial class FoodFactsLog
{
    [LoggerMessage(EventId = 3350, Level = LogLevel.Warning, Message = "Open Food Facts lookup unavailable and nothing cached")]
    public static partial void LookupUnavailable(ILogger logger);

    [LoggerMessage(EventId = 3351, Level = LogLevel.Information, Message = "Open Food Facts unavailable; served an expired cache entry")]
    public static partial void ServedStale(ILogger logger);
}

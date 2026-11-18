using Microsoft.Extensions.Logging;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>Log events of the Open Food Facts client (Infrastructure range 2450–2499). Never the barcode or response content.</summary>
internal static partial class OpenFoodFactsLog
{
    /// <param name="reason"><c>disabled</c>, <c>throttled</c>, <c>timeout</c>, <c>network</c>, <c>invalid_response</c> or <c>http_&lt;status&gt;</c>.</param>
    [LoggerMessage(EventId = 2450, Level = LogLevel.Warning, Message = "Open Food Facts request failed: {Reason}")]
    public static partial void FetchFailed(ILogger logger, string reason);
}

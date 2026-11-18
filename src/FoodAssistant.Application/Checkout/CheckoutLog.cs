using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Checkout;

/// <summary>Log events of completing a shopping list (Application range 3500–3549). Only ids and counts.</summary>
internal static partial class CheckoutLog
{
    [LoggerMessage(EventId = 3500, Level = LogLevel.Information,
        Message = "Shopping list {ListId} completed: {BatchCount} batches, {SkippedCount} skipped, {StaleCount} stale")]
    public static partial void CheckoutCompleted(ILogger logger, Guid listId, int batchCount, int skippedCount, int staleCount);

    [LoggerMessage(EventId = 3501, Level = LogLevel.Warning,
        Message = "A stock-changed handler failed after a checkout; the checkout stays saved")]
    public static partial void StockHandlerFailed(ILogger logger, Exception exception);
}

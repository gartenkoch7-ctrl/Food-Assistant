using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Inventory;

/// <summary>Log events of the inventory feature (Application range 3300–3349). Only ids and kinds, no names.</summary>
internal static partial class InventoryLog
{
    [LoggerMessage(EventId = 3300, Level = LogLevel.Information,
        Message = "Batch {BatchId} of product {ProductId} added ({Origin})")]
    public static partial void BatchAdded(ILogger logger, Guid batchId, Guid productId, BatchOrigin origin);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "Batch {BatchId} changed: {Change}")]
    public static partial void BatchStateChanged(ILogger logger, Guid batchId, string change);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information, Message = "Batch {BatchId} consumed ({Kind})")]
    public static partial void BatchConsumed(ILogger logger, Guid batchId, ConsumptionKind kind);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Information, Message = "Activity {ActivityId} undone")]
    public static partial void UndoApplied(ILogger logger, Guid activityId);

    [LoggerMessage(EventId = 3304, Level = LogLevel.Information, Message = "Undo refused: {Reason}")]
    public static partial void UndoRejected(ILogger logger, string reason);

    [LoggerMessage(EventId = 3305, Level = LogLevel.Warning,
        Message = "A stock-changed handler failed; the inventory change stays saved")]
    public static partial void StockHandlerFailed(ILogger logger, Exception exception);
}

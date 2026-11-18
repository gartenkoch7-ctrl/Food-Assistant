using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Catalog;

/// <summary>Log events of the catalog feature (Application range 3200–3249). Only ids, no names or barcodes.</summary>
internal static partial class CatalogLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information, Message = "Product {ProductId} created in household {HouseholdId}")]
    public static partial void ProductCreated(ILogger logger, Guid productId, Guid householdId);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information, Message = "Product {ProductId} archived in household {HouseholdId}")]
    public static partial void ProductArchived(ILogger logger, Guid productId, Guid householdId);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Storage location {LocationId} created in household {HouseholdId}")]
    public static partial void StorageLocationCreated(ILogger logger, Guid locationId, Guid householdId);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "Storage location {LocationId} deleted in household {HouseholdId}")]
    public static partial void StorageLocationDeleted(ILogger logger, Guid locationId, Guid householdId);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Warning, Message = "A stock-changed handler failed after the minimum stock of product {ProductId} changed")]
    public static partial void StockHandlerFailed(ILogger logger, Guid productId, Exception exception);
}

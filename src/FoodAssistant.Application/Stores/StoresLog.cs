using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Stores;

/// <summary>Log events of the stores feature (Application range 3450–3499). Only ids, no names.</summary>
internal static partial class StoresLog
{
    [LoggerMessage(EventId = 3450, Level = LogLevel.Information, Message = "Store {StoreId} created")]
    public static partial void StoreCreated(ILogger logger, Guid storeId);

    [LoggerMessage(EventId = 3451, Level = LogLevel.Information, Message = "Store {StoreId} deleted")]
    public static partial void StoreDeleted(ILogger logger, Guid storeId);

    [LoggerMessage(EventId = 3452, Level = LogLevel.Information, Message = "Manual section order of store {StoreId} set")]
    public static partial void SectionOrderSet(ILogger logger, Guid storeId);

    [LoggerMessage(EventId = 3453, Level = LogLevel.Information, Message = "Section order of store {StoreId} reset to the learned order")]
    public static partial void SectionOrderReset(ILogger logger, Guid storeId);
}

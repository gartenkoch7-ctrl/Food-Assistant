using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>Log events of the shelf-life feature (Application range 3250–3299). Only ids and categories, no values.</summary>
internal static partial class ShelfLifeLog
{
    [LoggerMessage(EventId = 3250, Level = LogLevel.Information,
        Message = "Shelf-life override ({Scope}, {StorageType}) of household {HouseholdId} changed")]
    public static partial void OverrideChanged(ILogger logger, Guid householdId, string scope, StorageType storageType);

    [LoggerMessage(EventId = 3251, Level = LogLevel.Information,
        Message = "Shelf-life override ({Scope}, {StorageType}) of household {HouseholdId} reset to default")]
    public static partial void OverrideReset(ILogger logger, Guid householdId, string scope, StorageType storageType);
}

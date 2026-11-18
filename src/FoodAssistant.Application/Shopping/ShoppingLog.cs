using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Shopping;

/// <summary>Log events of the shopping feature (Application range 3400–3449). Only ids, no texts.</summary>
internal static partial class ShoppingLog
{
    [LoggerMessage(EventId = 3400, Level = LogLevel.Information,
        Message = "Conflict on shopping item {ItemId} resolved: {Outcome}")]
    public static partial void ShoppingConflictResolved(ILogger logger, Guid itemId, ShoppingConflictOutcome outcome);

    [LoggerMessage(EventId = 3401, Level = LogLevel.Information, Message = "Shopping list {ListId} created")]
    public static partial void ShoppingListCreated(ILogger logger, Guid listId);

    [LoggerMessage(EventId = 3402, Level = LogLevel.Information, Message = "Shopping list {ListId} deleted")]
    public static partial void ShoppingListDeleted(ILogger logger, Guid listId);

    [LoggerMessage(EventId = 3403, Level = LogLevel.Information,
        Message = "Change of a shopping item refused: a restricted member may only check items off")]
    public static partial void RestrictedChangeDenied(ILogger logger);
}

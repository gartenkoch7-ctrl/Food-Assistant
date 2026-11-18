using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Restock;

/// <summary>Log events of the restock feature (Application range 3550–3599). Only ids, no names.</summary>
internal static partial class RestockLog
{
    [LoggerMessage(EventId = 3550, Level = LogLevel.Information,
        Message = "Product {ProductId} fell below its minimum stock; added to list {ListId}")]
    public static partial void RestockItemAdded(ILogger logger, Guid productId, Guid listId);

    [LoggerMessage(EventId = 3551, Level = LogLevel.Information,
        Message = "Recurring rule {RuleId} added an entry to list {ListId}")]
    public static partial void RecurringItemAdded(ILogger logger, Guid ruleId, Guid listId);

    [LoggerMessage(EventId = 3552, Level = LogLevel.Information,
        Message = "Recurring rule {RuleId} skipped: its last entry is still open")]
    public static partial void RecurringItemSkipped(ILogger logger, Guid ruleId);

    [LoggerMessage(EventId = 3553, Level = LogLevel.Warning,
        Message = "Recurring run failed for household {HouseholdId}")]
    public static partial void RecurringRunFailed(ILogger logger, Guid householdId, Exception exception);

    [LoggerMessage(EventId = 3555, Level = LogLevel.Warning,
        Message = "Recurring pass over all households failed; the next pass runs on schedule")]
    public static partial void RecurringJobFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3556, Level = LogLevel.Warning,
        Message = "Recurring rule {RuleId} failed; it moves on to its next due date")]
    public static partial void RecurringRuleFailed(ILogger logger, Guid ruleId, Exception exception);

    [LoggerMessage(EventId = 3554, Level = LogLevel.Warning,
        Message = "Minimum-stock check of product {ProductId} failed")]
    public static partial void MinimumStockCheckFailed(ILogger logger, Guid productId, Exception exception);
}

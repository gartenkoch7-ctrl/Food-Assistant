using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Restock;

/// <summary>A validated recurring rule to store.</summary>
/// <param name="ListId">Existing list of the household.</param>
/// <param name="ProductId">Existing active product, or <see langword="null"/>.</param>
/// <param name="Text">Trimmed text; the product name for product rules.</param>
/// <param name="Quantity">Optional amount.</param>
/// <param name="Frequency">How often.</param>
/// <param name="DayOfWeek">Set for weekly rules.</param>
/// <param name="DayOfMonth">Set for monthly rules.</param>
/// <param name="NextDueOn">First due date after a change of the schedule; kept by the store if the schedule is unchanged.</param>
public sealed record RecurringRuleData(
    Guid ListId,
    Guid? ProductId,
    string Text,
    Quantity? Quantity,
    RecurrenceFrequency Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    DateOnly NextDueOn);

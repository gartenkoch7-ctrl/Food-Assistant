using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Restock;

/// <summary>A rule whose due date has come, with what the runner needs to act on it.</summary>
public sealed record DueRecurringRule(
    Guid Id,
    Guid ListId,
    Guid? ProductId,
    string Text,
    Quantity? Quantity,
    RecurrenceFrequency Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    Guid? LastCreatedItemId,
    DateOnly DueOn,
    long Version);

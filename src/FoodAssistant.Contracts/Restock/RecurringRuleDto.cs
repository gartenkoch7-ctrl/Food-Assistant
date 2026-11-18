using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Restock;

/// <summary>A rule that adds an entry to a shopping list on a schedule.</summary>
/// <param name="Id">Rule id (UUIDv7).</param>
/// <param name="ListId">List that receives the entries.</param>
/// <param name="ProductId">Linked product, or <see langword="null"/> for free text.</param>
/// <param name="Text">Free text, or the product name at the time the rule was saved.</param>
/// <param name="Quantity">Optional amount of each entry.</param>
/// <param name="Frequency">Daily, weekly or monthly.</param>
/// <param name="DayOfWeek">Weekday of weekly rules, otherwise <see langword="null"/>.</param>
/// <param name="DayOfMonth">Day (1–31) of monthly rules, otherwise <see langword="null"/>; short months use their last day.</param>
/// <param name="NextDueOn">Household date of the next entry.</param>
/// <param name="Version">Version, also sent as <c>ETag</c>.</param>
public sealed record RecurringRuleDto(
    Guid Id,
    Guid ListId,
    Guid? ProductId,
    string Text,
    QuantityDto? Quantity,
    RecurrenceFrequencyDto Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    DateOnly NextDueOn,
    long Version);

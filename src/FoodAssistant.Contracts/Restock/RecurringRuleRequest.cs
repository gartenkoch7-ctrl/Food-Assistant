using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Restock;

/// <summary>Creates or replaces a recurring rule (<c>POST</c> without, <c>PUT</c> with <c>If-Match</c>).</summary>
/// <param name="Id">Client-generated UUIDv7 for <c>POST</c>, or <see langword="null"/>; ignored by <c>PUT</c>.</param>
/// <param name="ListId">List of the household that receives the entries.</param>
/// <param name="ProductId">Product of the household; the entry then uses its name and section.</param>
/// <param name="Text">Free text (1–200 characters); needed without a product, otherwise ignored.</param>
/// <param name="Quantity">Optional amount (value &gt; 0).</param>
/// <param name="Frequency">Daily, weekly or monthly.</param>
/// <param name="DayOfWeek">Required for weekly rules.</param>
/// <param name="DayOfMonth">Required for monthly rules (1–31).</param>
public sealed record RecurringRuleRequest(
    Guid? Id,
    Guid ListId,
    Guid? ProductId,
    string? Text,
    QuantityDto? Quantity,
    RecurrenceFrequencyDto Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth);

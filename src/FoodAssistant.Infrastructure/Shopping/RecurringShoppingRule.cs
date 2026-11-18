using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>Adds an entry to a shopping list on a regular schedule ("jede Woche Milch").</summary>
public sealed class RecurringShoppingRule : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Guid ListId { get; set; }

    public Guid? ProductId { get; set; }

    public required string Text { get; set; }

    public decimal? QuantityValue { get; set; }

    /// <summary>Unit code (<c>Unit.Code</c>); set together with <see cref="QuantityValue"/>.</summary>
    public string? QuantityUnitCode { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Weekday (<see cref="System.DayOfWeek"/> as number) for <see cref="RecurrenceFrequency.Weekly"/>.</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>Day of month for <see cref="RecurrenceFrequency.Monthly"/>.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Household date on which the next entry is added.</summary>
    public DateOnly NextDueOn { get; set; }

    /// <summary>Entry created last time; no foreign key, the entry may be gone.</summary>
    public Guid? LastCreatedItemId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

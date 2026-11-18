using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Inventory;

/// <summary>
/// One use or disposal of stock, kept for statistics and undo. Events are never edited, only
/// marked as undone, so there is no <c>UpdatedAt</c>.
/// </summary>
public sealed class ConsumptionEvent : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Guid ProductId { get; init; }

    /// <summary>The batch at the time of the event; no foreign key, the batch may be gone later.</summary>
    public Guid? BatchId { get; init; }

    public ConsumptionKind Kind { get; init; }

    public ConsumptionReason? Reason { get; init; }

    public decimal QuantityValue { get; init; }

    /// <summary>Unit code (<c>Unit.Code</c>) of <see cref="QuantityValue"/>.</summary>
    public required string QuantityUnitCode { get; init; }

    /// <summary>Expiry status when the event happened (waste statistics).</summary>
    public ExpiryStatus ExpiryStatusAtEvent { get; init; }

    /// <summary>Who recorded it; no foreign key, history outlives deleted accounts.</summary>
    public Guid? UserId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Household date of <see cref="OccurredAt"/> (time zone of the household).</summary>
    public DateOnly OccurredOn { get; init; }

    public DateTimeOffset? UndoneAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Optimistic concurrency token (<c>xmin</c>); guards concurrent undo.</summary>
    public uint Version { get; set; }
}

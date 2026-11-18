using FoodAssistant.Application.Activity;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Activity;

/// <summary>
/// One entry of the household activity feed ("Verlauf"). Holds only ids and keys; names are
/// joined when the feed is read, so renamed products and deleted accounts show correctly.
/// </summary>
public sealed class HouseholdActivity : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public ActivityArea Area { get; init; }

    /// <summary>Stable kind key of the area, e.g. <c>batch_opened</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The changed entity (batch, shopping entry, product …).</summary>
    public Guid SubjectId { get; init; }

    /// <summary>Product concerned; no foreign key, the feed outlives the data.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Who did it; no foreign key, null for the system.</summary>
    public Guid? UserId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Data needed for undo (e.g. the previous batch state); never shown or logged.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Version of the subject right after the change; undo is refused if it changed since.</summary>
    public long? SubjectVersionAfter { get; init; }

    public DateTimeOffset? UndoneAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (<c>xmin</c>); guards concurrent undo.</summary>
    public uint Version { get; set; }
}

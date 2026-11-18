using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Sync;

/// <summary>
/// One change of a <see cref="ISyncTracked"/> entity (ADR-0005). Clients fetch the changes after
/// the last <see cref="Sequence"/> they know; written only by the sync interceptor.
/// </summary>
public sealed class ChangeLogEntry : IHouseholdOwned
{
    /// <summary>Database-generated, strictly increasing across all households.</summary>
    public long Sequence { get; init; }

    public Guid HouseholdId { get; init; }

    /// <summary>A <c>SyncEntityTypes</c> value.</summary>
    public required string EntityType { get; init; }

    public Guid EntityId { get; init; }

    public bool Deleted { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}

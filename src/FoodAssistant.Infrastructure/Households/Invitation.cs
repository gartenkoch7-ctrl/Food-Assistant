using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Households;

/// <summary>
/// Invitation into a household with a fixed role: single use, 7 days valid, revocable (ADR-0002).
/// Only the SHA-256 hash of the token is stored.
/// </summary>
public sealed class Invitation : IHouseholdOwned
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Household? Household { get; init; }

    public HouseholdRole Role { get; init; }

    /// <summary>Lower-case hex SHA-256 of the token; unique, used for lookup.</summary>
    public required string TokenHash { get; init; }

    /// <summary>Owner who created it; cleared when that account is deleted.</summary>
    public Guid? CreatedByUserId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Optimistic concurrency token (<c>xmin</c>); makes redemption single-use under races.</summary>
    public uint Version { get; set; }
}

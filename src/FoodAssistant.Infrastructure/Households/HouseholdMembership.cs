using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Accounts;

namespace FoodAssistant.Infrastructure.Households;

/// <summary>
/// A person's membership in a household with a role. Deliberately not <see cref="Persistence.IHouseholdOwned"/>:
/// memberships are the access relation itself and are queried per person across households
/// (e.g. "my households"); every query constrains household and user explicitly.
/// </summary>
public sealed class HouseholdMembership
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Household? Household { get; init; }

    public Guid UserId { get; init; }

    public User? User { get; init; }

    public HouseholdRole Role { get; set; }

    public DateTimeOffset JoinedAt { get; init; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL <c>xmin</c>.</summary>
    public uint Version { get; set; }
}

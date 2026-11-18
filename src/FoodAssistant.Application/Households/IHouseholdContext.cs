namespace FoodAssistant.Application.Households;

/// <summary>
/// The household the current request works in, together with the caller's membership
/// (ADR-0002). The server sets it from the route <c>/api/v1/households/{householdId}/…</c> after
/// checking the membership; outside such routes, or without membership, nothing is active.
/// </summary>
/// <remarks>
/// Persistence uses <see cref="HouseholdId"/> as EF Core query filter for every household-owned
/// entity. No active household means such queries return nothing (fail closed).
/// </remarks>
public interface IHouseholdContext
{
    /// <summary>Id of the active household, or <see langword="null"/> when none is active.</summary>
    Guid? HouseholdId { get; }

    /// <summary>Membership of the caller in the active household, or <see langword="null"/>.</summary>
    HouseholdMembershipInfo? Membership { get; }
}

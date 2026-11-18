using FoodAssistant.Application.Households;

namespace FoodAssistant.Server.Households;

/// <summary>
/// Request-scoped <see cref="IHouseholdContext"/>. Filled by the household authorization handler
/// after the membership check; until then (and for non-members) no household is active.
/// Deliberately free of dependencies: the database context depends on it.
/// </summary>
public sealed class HttpHouseholdContext : IHouseholdContext
{
    public Guid? HouseholdId => Membership?.HouseholdId;

    public HouseholdMembershipInfo? Membership { get; private set; }

    /// <summary>Whether the membership lookup for this request already happened.</summary>
    public bool IsResolved { get; private set; }

    public void SetMembership(HouseholdMembershipInfo? membership)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("The household of a request can only be set once.");
        }

        Membership = membership;
        IsResolved = true;
    }
}

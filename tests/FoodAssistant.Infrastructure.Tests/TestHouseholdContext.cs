using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Household context a test can switch, standing in for the server's route-based one.</summary>
public sealed class TestHouseholdContext : IHouseholdContext
{
    public Guid? HouseholdId => Membership?.HouseholdId;

    public HouseholdMembershipInfo? Membership { get; private set; }

    public void Activate(Guid householdId) =>
        Membership = new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner);

    public void Clear() => Membership = null;
}

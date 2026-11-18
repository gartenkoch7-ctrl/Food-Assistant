using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>"Every household keeps at least one owner" (ADR-0002).</summary>
public static class HouseholdOwnershipRules
{
    /// <summary>
    /// Whether changing a member from <paramref name="currentRole"/> to <paramref name="newRole"/>
    /// (or removing them, <paramref name="newRole"/> = <see langword="null"/>) leaves no owner.
    /// </summary>
    /// <param name="ownerCount">Number of owners before the change, counted under a lock.</param>
    public static bool LeavesNoOwner(HouseholdRole currentRole, HouseholdRole? newRole, int ownerCount) =>
        currentRole == HouseholdRole.Owner && newRole != HouseholdRole.Owner && ownerCount <= 1;
}

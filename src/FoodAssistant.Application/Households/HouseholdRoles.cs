using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>
/// Role hierarchy of ADR-0002: Owner ⊇ Member ⊇ Restricted. The only place where roles are
/// compared; endpoints use policies built on top of this.
/// </summary>
public static class HouseholdRoles
{
    /// <summary>Whether <paramref name="actual"/> grants at least the rights of <paramref name="minimum"/>.</summary>
    public static bool Satisfies(HouseholdRole actual, HouseholdRole minimum) => Rank(actual) >= Rank(minimum);

    /// <summary>Whether the value is one of the defined roles (guards against undefined enum values).</summary>
    public static bool IsDefined(HouseholdRole role) =>
        role is HouseholdRole.Owner or HouseholdRole.Member or HouseholdRole.Restricted;

    private static int Rank(HouseholdRole role) => role switch
    {
        HouseholdRole.Owner => 3,
        HouseholdRole.Member => 2,
        HouseholdRole.Restricted => 1,
        _ => 0,
    };
}

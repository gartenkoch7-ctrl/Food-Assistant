namespace FoodAssistant.Server.Households;

/// <summary>Extension point for track K (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class HouseholdSettingsEndpoints
{
    public static RouteGroupBuilder MapHouseholdSettingsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

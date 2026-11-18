namespace FoodAssistant.Server.Households;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class HouseholdImportEndpoints
{
    public static RouteGroupBuilder MapHouseholdImportEndpoints(this RouteGroupBuilder households)
    {
        ArgumentNullException.ThrowIfNull(households);
        return households;
    }
}

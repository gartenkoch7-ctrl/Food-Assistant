namespace FoodAssistant.Server.Stores;

/// <summary>Extension point for track D (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class StoresEndpoints
{
    public static RouteGroupBuilder MapStoresEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

namespace FoodAssistant.Server.Restock;

/// <summary>Extension point for track G (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class RestockEndpoints
{
    public static RouteGroupBuilder MapRestockEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

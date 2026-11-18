namespace FoodAssistant.Server.ShelfLife;

/// <summary>Extension point for track B1 (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class ShelfLifeEndpoints
{
    public static RouteGroupBuilder MapShelfLifeEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

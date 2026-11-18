namespace FoodAssistant.Server.Inventory;

/// <summary>Extension point for track B2 (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

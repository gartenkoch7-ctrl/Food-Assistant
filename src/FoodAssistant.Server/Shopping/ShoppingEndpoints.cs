namespace FoodAssistant.Server.Shopping;

/// <summary>Extension point for track D (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class ShoppingEndpoints
{
    public static RouteGroupBuilder MapShoppingEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

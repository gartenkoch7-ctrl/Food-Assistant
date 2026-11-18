namespace FoodAssistant.Server.Catalog;

/// <summary>Extension point for track A (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

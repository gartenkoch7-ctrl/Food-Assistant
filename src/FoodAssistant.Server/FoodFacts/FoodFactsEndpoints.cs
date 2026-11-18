namespace FoodAssistant.Server.FoodFacts;

/// <summary>Extension point for track C1 (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class FoodFactsEndpoints
{
    public static RouteGroupBuilder MapFoodFactsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

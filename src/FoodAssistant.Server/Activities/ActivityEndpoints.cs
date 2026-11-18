namespace FoodAssistant.Server.Activities;

/// <summary>Extension point for track V (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class ActivityEndpoints
{
    public static RouteGroupBuilder MapActivityEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

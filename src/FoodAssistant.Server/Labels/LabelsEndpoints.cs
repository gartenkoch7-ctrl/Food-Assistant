namespace FoodAssistant.Server.Labels;

/// <summary>Extension point for track J (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class LabelsEndpoints
{
    public static RouteGroupBuilder MapLabelsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

namespace FoodAssistant.Server.Statistics;

/// <summary>Extension point for track I (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class StatisticsEndpoints
{
    public static RouteGroupBuilder MapStatisticsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

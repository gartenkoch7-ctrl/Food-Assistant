namespace FoodAssistant.Server.Sync;

/// <summary>Extension point for track S0 (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class SyncEndpoints
{
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

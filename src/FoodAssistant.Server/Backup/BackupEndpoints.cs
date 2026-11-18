namespace FoodAssistant.Server.Backup;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class BackupEndpoints
{
    public static RouteGroupBuilder MapBackupEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return api;
    }
}

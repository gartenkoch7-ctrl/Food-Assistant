namespace FoodAssistant.Server.Export;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class ExportEndpoints
{
    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

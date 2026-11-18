namespace FoodAssistant.Server.Import;

/// <summary>Extension point for track L (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class ImportEndpoints
{
    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

namespace FoodAssistant.Server.Notifications;

/// <summary>Extension point for track H (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class NotificationsEndpoints
{
    public static RouteGroupBuilder MapNotificationsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}

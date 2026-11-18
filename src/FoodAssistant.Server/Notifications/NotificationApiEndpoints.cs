namespace FoodAssistant.Server.Notifications;

/// <summary>Extension point for track H (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class NotificationApiEndpoints
{
    public static RouteGroupBuilder MapNotificationApiEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return api;
    }
}

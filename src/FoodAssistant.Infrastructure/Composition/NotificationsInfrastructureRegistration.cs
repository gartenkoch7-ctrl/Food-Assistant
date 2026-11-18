using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track H (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class NotificationsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddNotificationsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track V (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ActivityInfrastructureRegistration
{
    public static IHostApplicationBuilder AddActivityPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

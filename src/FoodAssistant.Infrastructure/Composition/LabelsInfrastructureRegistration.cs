using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track J (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class LabelsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddLabelsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

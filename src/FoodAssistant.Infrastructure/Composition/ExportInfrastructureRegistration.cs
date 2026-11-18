using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ExportInfrastructureRegistration
{
    public static IHostApplicationBuilder AddExportPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track L (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class ImportInfrastructureRegistration
{
    public static IHostApplicationBuilder AddImportPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

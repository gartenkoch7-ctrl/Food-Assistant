using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Extension point for track N (docs/plan/m01-mvp.md). Empty until the feature is implemented.</summary>
public static class BackupInfrastructureRegistration
{
    public static IHostApplicationBuilder AddBackupPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

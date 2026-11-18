using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Registers persistence and external providers. Each feature adds one line here (hotspot, additive only).</summary>
public static class InfrastructureRegistration
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddPersistence();
        builder.AddAccountsPersistence();
        builder.AddHouseholdDataPersistence();
        return builder;
    }
}

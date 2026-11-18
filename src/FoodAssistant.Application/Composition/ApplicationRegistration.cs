using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Registers application services. Each feature adds one line here (hotspot, additive only).</summary>
public static class ApplicationRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAccountsAndHouseholds();
        return services;
    }
}

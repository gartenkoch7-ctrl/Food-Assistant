using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Composition;

/// <summary>Registers application services. Each feature adds one line here (hotspot, additive only).</summary>
public static class ApplicationRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHouseholdData();
        services.AddCatalog();
        services.AddShelfLife();
        services.AddInventory();
        services.AddFoodFacts();
        services.AddShopping();
        services.AddCheckout();
        services.AddRestock();
        services.AddNotifications();
        services.AddStatistics();
        services.AddLabels();
        services.AddImport();
        services.AddBackup();
        services.AddExport();
        services.AddAccountsAndHouseholds();
        return services;
    }
}

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
        builder.AddCatalogPersistence();
        builder.AddShelfLifePersistence();
        builder.AddInventoryPersistence();
        builder.AddFoodFactsPersistence();
        builder.AddShoppingPersistence();
        builder.AddCheckoutPersistence();
        builder.AddRestockPersistence();
        builder.AddNotificationsPersistence();
        builder.AddStatisticsPersistence();
        builder.AddLabelsPersistence();
        builder.AddImportPersistence();
        builder.AddBackupPersistence();
        builder.AddExportPersistence();
        builder.AddActivityPersistence();
        return builder;
    }
}

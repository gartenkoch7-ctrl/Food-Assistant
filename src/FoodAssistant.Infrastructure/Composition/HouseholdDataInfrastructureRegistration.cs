using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Infrastructure.Households;
using FoodAssistant.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Seams shared by all household-data features: sync interceptors, calendar, activity feed.</summary>
public static class HouseholdDataInfrastructureRegistration
{
    public static IHostApplicationBuilder AddHouseholdDataPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        // Scoped like AppDbContext: one set per context, resolved in AddPersistence's options callback.
        services.AddScoped<PendingChangeNotifications>();
        services.AddScoped<SyncChangePublisher>();
        services.AddScoped<SyncChangeInterceptor>();
        services.AddScoped<SyncTransactionInterceptor>();

        services.AddScoped<IHouseholdCalendar, HouseholdCalendar>();
        services.AddScoped<IActivityLog, ActivityLog>();
        return builder;
    }
}

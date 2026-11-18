using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Application.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Application.Composition;

/// <summary>
/// Defaults of the seams shared by all household-data features (M1 step 0). Registered with
/// <c>TryAdd</c>: the server and later tracks replace them with a plain <c>Add</c>, which wins.
/// </summary>
public static class HouseholdDataRegistration
{
    public static IServiceCollection AddHouseholdData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHouseholdChangeNotifier, NullHouseholdChangeNotifier>();
        services.TryAddScoped<IShelfLifeRules, NoShelfLifeRules>();
        return services;
    }
}

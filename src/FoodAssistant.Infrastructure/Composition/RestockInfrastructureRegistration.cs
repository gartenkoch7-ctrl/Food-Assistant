using FoodAssistant.Application.Restock;
using FoodAssistant.Infrastructure.Restock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Persistence of minimum stock and recurring shopping rules (M1 track G).</summary>
public static class RestockInfrastructureRegistration
{
    public static IHostApplicationBuilder AddRestockPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IRestockStore, RestockStore>();
        return builder;
    }
}

using FoodAssistant.Application.Composition;
using FoodAssistant.Application.ShelfLife;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Application.Tests.ShelfLife;

public sealed class ShelfLifeRegistrationTests
{
    [Fact]
    public void AddApplication_ReplacesThePlaceholderWithTheRealRules()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        // The container resolves the last registration of a service type.
        var last = services.Last(d => d.ServiceType == typeof(IShelfLifeRules));
        Assert.Equal(typeof(ShelfLifeRuleService), last.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, last.Lifetime);
    }
}

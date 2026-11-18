using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Households;

/// <summary>Background work sees exactly one household, through the normal query filter.</summary>
public sealed class HouseholdScopeFactoryTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Scope_SeesOnlyItsHousehold_AsSystemOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var other = await Scenario.CreateHouseholdAsync(admin, "Other");
        var mine = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId, "Milk");
        await HouseholdDataSeed.ProductAsync(app.Services, other, "Bread");

        var factory = app.Services.GetRequiredService<IHouseholdScopeFactory>();
        await using var scope = factory.CreateScope(setup.HouseholdId);
        var products = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Set<Product>()
            .Select(p => p.Id)
            .ToListAsync(ct);
        var membership = scope.ServiceProvider.GetRequiredService<IHouseholdContext>().Membership;

        Assert.Equal([mine.Id], products);
        Assert.Equal(new HouseholdMembershipInfo(setup.HouseholdId, HouseholdScopes.SystemUserId, HouseholdRole.Owner), membership);
    }
}

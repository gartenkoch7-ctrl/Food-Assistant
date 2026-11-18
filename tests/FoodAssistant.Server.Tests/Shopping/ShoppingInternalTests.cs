using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Shopping;

/// <summary>The seams other tracks use without HTTP: default list and internally created entries (track G).</summary>
public sealed class ShoppingInternalTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddInternal_OnTheDefaultList_IsIdempotentAndKeepsItsOrigin()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var item = new NewShoppingItem(
            Guid.CreateVersion7(), null, " Milk ", new Quantity(2m, Unit.Litre), " ", StoreSections.DairyChilled, ShoppingItemOrigin.MinimumStock, null);

        var (list, first, second) = await api.WithScopeAsync(async services =>
        {
            var defaultList = await services.GetRequiredService<ShoppingListService>().EnsureDefaultListAsync(api.HouseholdId, Ct);
            var items = services.GetRequiredService<ShoppingItemService>();
            var added = await items.AddInternalAsync(api.HouseholdId, defaultList.Id, item, Ct);
            var again = await items.AddInternalAsync(api.HouseholdId, defaultList.Id, item, Ct);
            return (defaultList, added, again);
        });

        Assert.True(list.IsDefault);
        Assert.Equal(("Milk", (string?)null, ShoppingItemOriginDto.MinimumStock), (first.Text, first.Note, first.Origin));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Id, Assert.Single(await api.ItemsAsync(list.Id)).Id);
    }

    [Fact]
    public async Task AddInternal_InvalidItemOrUnknownList_Throws()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var item = new NewShoppingItem(
            Guid.CreateVersion7(), null, "Milk", null, null, StoreSections.DairyChilled, ShoppingItemOrigin.Recurring, null);

        await api.WithScopeAsync(async services =>
        {
            var items = services.GetRequiredService<ShoppingItemService>();
            var list = await services.GetRequiredService<ShoppingListService>().EnsureDefaultListAsync(api.HouseholdId, Ct);
            await Assert.ThrowsAsync<ArgumentException>(
                () => items.AddInternalAsync(api.HouseholdId, list.Id, item with { SectionKey = "aisle" }, Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => items.AddInternalAsync(api.HouseholdId, Guid.CreateVersion7(), item, Ct));
            return true;
        });
    }
}

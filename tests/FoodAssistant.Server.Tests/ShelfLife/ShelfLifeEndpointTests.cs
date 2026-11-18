using System.Net;
using System.Text.Json;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.ShelfLife;

public sealed class ShelfLifeEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly ShelfLifeDaysDto None = new(null, null, null, null);

    [Fact]
    public async Task Rules_ListEveryCategoryAndStorageType_WithEmptyShippedTable()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        var rules = await admin.GetJsonAsync<List<ShelfLifeRuleRowDto>>(Base(setup.HouseholdId) + "/shelf-life/rules");
        var sources = await admin.GetJsonAsync<List<ShelfLifeSourceDto>>(Base(setup.HouseholdId) + "/shelf-life/sources");

        Assert.Equal(ProductCategories.All.Count * 3, rules.Count);
        Assert.All(rules, r => Assert.Equal((None, null, None, null), (r.Default, r.Override, r.Effective, r.SourceKey)));
        Assert.Empty(sources);
        Assert.Equal(ShelfLifeRule.Empty, await ResolveAsync(app, setup.HouseholdId, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task CategoryOverride_SetChangesListAndRules_ResetRemovesIt()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var milk = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        var url = Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled";

        using (var put = await admin.PutAsync(url, new ShelfLifeDaysDto(3, null, 1, 2)))
        {
            var row = await TestClient.ReadAsync<ShelfLifeRuleRowDto>(put);
            Assert.Equal((ProductCategories.Dairy, StorageTypeDto.Chilled), (row.CategoryKey, row.StorageType));
            Assert.Equal(new ShelfLifeDaysDto(3, null, 1, 2), row.Override);
            Assert.Equal(new ShelfLifeDaysDto(3, null, 1, 2), row.Effective);
        }

        var rules = await admin.GetJsonAsync<List<ShelfLifeRuleRowDto>>(Base(setup.HouseholdId) + "/shelf-life/rules");
        Assert.Single(rules, r => r.Override is not null);
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 3, DaysAfterThawing = 1, DaysAsLeftover = 2 }, await ResolveAsync(app, setup.HouseholdId, milk.Id));

        using (var delete = await admin.DeleteAsync(url))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        rules = await admin.GetJsonAsync<List<ShelfLifeRuleRowDto>>(Base(setup.HouseholdId) + "/shelf-life/rules");
        Assert.All(rules, r => Assert.Null(r.Override));
        Assert.Equal(ShelfLifeRule.Empty, await ResolveAsync(app, setup.HouseholdId, milk.Id));
    }

    [Fact]
    public async Task EmptyOverride_ActsAsReset()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var url = Base(setup.HouseholdId) + "/shelf-life/overrides/categories/fish/frozen";
        using (await admin.PutAsync(url, new ShelfLifeDaysDto(null, 90, null, null)))
        {
        }

        using var empty = await admin.PutAsync(url, None);

        var row = await TestClient.ReadAsync<ShelfLifeRuleRowDto>(empty);
        Assert.Null(row.Override);
        var rules = await admin.GetJsonAsync<List<ShelfLifeRuleRowDto>>(Base(setup.HouseholdId) + "/shelf-life/rules");
        Assert.All(rules, r => Assert.Null(r.Override));
    }

    [Fact]
    public async Task ProductOverride_InheritsCategoryAndWinsPerValue()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var milk = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        var product = $"{Base(setup.HouseholdId)}/products/{milk.Id}/shelf-life";
        using (await admin.PutAsync(Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled", new ShelfLifeDaysDto(3, null, null, 2)))
        {
        }

        using (var put = await admin.PutAsync(product + "/chilled", new ShelfLifeDaysDto(5, null, null, null)))
        {
            var row = await TestClient.ReadAsync<ShelfLifeRuleRowDto>(put);
            Assert.Equal(new ShelfLifeDaysDto(3, null, null, 2), row.Default);
            Assert.Equal(new ShelfLifeDaysDto(5, null, null, 2), row.Effective);
        }

        var dto = await admin.GetJsonAsync<ProductShelfLifeDto>(product);
        Assert.Equal((milk.Id, ProductCategories.Dairy), (dto.ProductId, dto.CategoryKey));
        Assert.Equal([StorageTypeDto.Chilled, StorageTypeDto.Frozen, StorageTypeDto.Ambient], dto.Rows.Select(r => r.StorageType));
        Assert.Equal(new ShelfLifeDaysDto(5, null, null, null), dto.Rows[0].Override);
        Assert.Null(dto.Rows[1].Override);
        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 5, DaysAsLeftover = 2 }, await ResolveAsync(app, setup.HouseholdId, milk.Id));

        using (var delete = await admin.DeleteAsync(product + "/chilled"))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        Assert.Equal(new ShelfLifeRule { DaysAfterOpening = 3, DaysAsLeftover = 2 }, await ResolveAsync(app, setup.HouseholdId, milk.Id));
    }

    [Fact]
    public async Task InvalidInput_IsRejected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var milk = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        var categories = Base(setup.HouseholdId) + "/shelf-life/overrides/categories";

        using var negative = await admin.PutAsync($"{categories}/dairy/chilled", new ShelfLifeDaysDto(-1, 3651, null, null));
        using var unknownCategory = await admin.PutAsync($"{categories}/cake/chilled", new ShelfLifeDaysDto(1, null, null, null));
        using var unknownType = await admin.PutAsync($"{categories}/dairy/Chilled", new ShelfLifeDaysDto(1, null, null, null));
        using var productType = await admin.DeleteAsync($"{Base(setup.HouseholdId)}/products/{milk.Id}/shelf-life/0");

        await TestClient.AssertProblemAsync(negative, HttpStatusCode.BadRequest, ShelfLifeErrorCodes.ValidationFailed);
        await TestClient.AssertProblemAsync(unknownCategory, HttpStatusCode.NotFound, ShelfLifeErrorCodes.UnknownCategory);
        await TestClient.AssertProblemAsync(unknownType, HttpStatusCode.NotFound, ShelfLifeErrorCodes.UnknownStorageType);
        await TestClient.AssertProblemAsync(productType, HttpStatusCode.NotFound, ShelfLifeErrorCodes.UnknownStorageType);
    }

    [Fact]
    public async Task InvalidInput_NamesTheFields()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        using var response = await admin.PutAsync(
            Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled",
            new ShelfLifeDaysDto(-1, 3651, 0, null));

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.Equal(["daysAfterOpening", "daysFrozen"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Restricted_CanReadButNotWrite()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var milk = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        var (kid, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = kid;
        var product = $"{Base(setup.HouseholdId)}/products/{milk.Id}/shelf-life";

        Assert.NotEmpty(await kid.GetJsonAsync<List<ShelfLifeRuleRowDto>>(Base(setup.HouseholdId) + "/shelf-life/rules"));
        Assert.Equal(3, (await kid.GetJsonAsync<ProductShelfLifeDto>(product)).Rows.Count);
        using var setCategory = await kid.PutAsync(Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled", None);
        using var resetCategory = await kid.DeleteAsync(Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled");
        using var setProduct = await kid.PutAsync(product + "/chilled", None);
        using var resetProduct = await kid.DeleteAsync(product + "/chilled");

        foreach (var response in new[] { setCategory, resetCategory, setProduct, resetProduct })
        {
            await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        }
    }

    [Fact]
    public async Task ForeignHouseholdOrProduct_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;
        var foreignMilk = await HouseholdDataSeed.ProductAsync(app.Services, foreign);

        using var rules = await outsider.GetAsync(Base(setup.HouseholdId) + "/shelf-life/rules");
        using var write = await outsider.PutAsync(Base(setup.HouseholdId) + "/shelf-life/overrides/categories/dairy/chilled", None);
        using var getProduct = await admin.GetAsync($"{Base(setup.HouseholdId)}/products/{foreignMilk.Id}/shelf-life");
        using var setProduct = await admin.PutAsync($"{Base(setup.HouseholdId)}/products/{foreignMilk.Id}/shelf-life/chilled", new ShelfLifeDaysDto(1, null, null, null));
        using var resetProduct = await admin.DeleteAsync($"{Base(setup.HouseholdId)}/products/{foreignMilk.Id}/shelf-life/chilled");

        await TestClient.AssertProblemAsync(rules, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(write, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(getProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(setProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(resetProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
    }

    private static string Base(Guid householdId) => $"/api/v1/households/{householdId}";

    /// <summary>The rules B2 will use, resolved like in a request of the household.</summary>
    private static async Task<ShelfLifeRule> ResolveAsync(ServerTestApp app, Guid householdId, Guid productId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner));
        return await scope.ServiceProvider.GetRequiredService<IShelfLifeRules>()
            .ResolveAsync(productId, StorageType.Chilled, TestContext.Current.CancellationToken);
    }
}

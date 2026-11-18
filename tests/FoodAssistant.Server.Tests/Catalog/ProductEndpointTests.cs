using System.Net;
using System.Text.Json;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Catalog;

public sealed class ProductEndpointTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ReturnsCreatedWithEtagAndLocation_AndGetReturnsIt()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var request = CatalogApi.NewProduct("  Milk ", "4006381333931") with { Brand = "Farm", MinimumStock = new QuantityDto(2m, "l") };

        using var response = await admin.PostAsync(CatalogApi.Products(setup.HouseholdId), request);
        var created = await TestClient.ReadAsync<ProductDto>(response, HttpStatusCode.Created);

        Assert.Equal("Milk", created.Name);
        Assert.Equal(["4006381333931"], created.Barcodes);
        Assert.False(created.FromOpenFoodFacts);
        Assert.Equal(CatalogApi.ETag(created.Version), response.Headers.ETag?.Tag);
        Assert.EndsWith($"/products/{created.Id}", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        var loaded = await admin.GetJsonAsync<ProductDto>($"{CatalogApi.Products(setup.HouseholdId)}/{created.Id}");
        Assert.Equal((created.Id, created.Name, created.Version), (loaded.Id, loaded.Name, loaded.Version));
        Assert.Equal(created.Barcodes, loaded.Barcodes);
        Assert.Equal(new QuantityDto(2m, "l"), loaded.MinimumStock);
    }

    [Fact]
    public async Task Create_InvalidInput_ReturnsFieldErrors()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var request = CatalogApi.NewProduct(" ", "123") with { CategoryKey = "snacks", DefaultUnit = "gallon" };

        using var response = await admin.PostAsync(CatalogApi.Products(setup.HouseholdId), request);

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, CatalogErrorCodes.ValidationFailed);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("categoryKey", out _));
        Assert.True(errors.TryGetProperty("defaultUnit", out _));
        Assert.True(errors.TryGetProperty("barcodes", out _));
    }

    [Fact]
    public async Task Create_DuplicateBarcodeOrId_ReturnsConflict()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var first = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Milk", "4006381333931"));

        using var sameBarcode = await admin.PostAsync(CatalogApi.Products(setup.HouseholdId), CatalogApi.NewProduct("Other", "4006381333931"));
        using var sameId = await admin.PostAsync(CatalogApi.Products(setup.HouseholdId), CatalogApi.NewProduct("Other") with { Id = first.Id });

        await TestClient.AssertProblemAsync(sameBarcode, HttpStatusCode.Conflict, CatalogErrorCodes.BarcodeInUse);
        await TestClient.AssertProblemAsync(sameId, HttpStatusCode.Conflict, CatalogErrorCodes.ProductExists);
    }

    [Fact]
    public async Task Update_NeedsIfMatch_Stale412_Current200WithNewEtag()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var product = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct());
        var url = $"{CatalogApi.Products(setup.HouseholdId)}/{product.Id}";

        using var missing = await admin.PutAsync(url, CatalogApi.Update("Whole milk"));
        using var stale = await CatalogApi.SendAsync(admin, HttpMethod.Put, url, CatalogApi.Update("Whole milk"), CatalogApi.ETag(product.Version + 1));
        using var ok = await CatalogApi.SendAsync(admin, HttpMethod.Put, url, CatalogApi.Update("Whole milk", "12345678"), CatalogApi.ETag(product.Version));

        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        Assert.Equal(CatalogApi.ETag(product.Version), stale.Headers.ETag?.Tag);
        var updated = await TestClient.ReadAsync<ProductDto>(ok);
        Assert.Equal("Whole milk", updated.Name);
        Assert.Equal(["12345678"], updated.Barcodes);
        Assert.Equal(CatalogApi.ETag(updated.Version), ok.Headers.ETag?.Tag);
        Assert.NotEqual(product.Version, updated.Version);
    }

    [Fact]
    public async Task Archive_HidesProduct_AndIsRefusedWithStock()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var empty = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Butter", "12345678"));
        var stocked = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Milk"));
        var location = await HouseholdDataSeed.StorageLocationAsync(app.Services, setup.HouseholdId);
        await HouseholdDataSeed.BatchAsync(app.Services, setup.HouseholdId, stocked.Id, location.Id);
        var emptyUrl = $"{CatalogApi.Products(setup.HouseholdId)}/{empty.Id}";

        using var withoutHeader = await admin.DeleteAsync(emptyUrl);
        using var refused = await CatalogApi.DeleteAsync(admin, $"{CatalogApi.Products(setup.HouseholdId)}/{stocked.Id}", CatalogApi.ETag(stocked.Version));
        using var archived = await CatalogApi.DeleteAsync(admin, emptyUrl, CatalogApi.ETag(empty.Version));
        using var get = await admin.GetAsync(emptyUrl);
        using var byBarcode = await admin.GetAsync($"{CatalogApi.Products(setup.HouseholdId)}/by-barcode/12345678");

        await TestClient.AssertProblemAsync(withoutHeader, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(refused, HttpStatusCode.Conflict, CatalogErrorCodes.ProductHasStock);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        await TestClient.AssertProblemAsync(get, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(byBarcode, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
    }

    [Fact]
    public async Task Search_FiltersAndPagesWithCursor_AndFindsByBarcode()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        foreach (var name in new[] { "Cheese", "Apple", "Bread", "Apple juice" })
        {
            await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, name == "Bread" ? CatalogApi.NewProduct(name, "4006381333931") : CatalogApi.NewProduct(name));
        }

        var url = CatalogApi.Products(setup.HouseholdId);
        var first = await admin.GetJsonAsync<CursorPage<ProductSummaryDto>>($"{url}?limit=2");
        var second = await admin.GetJsonAsync<CursorPage<ProductSummaryDto>>($"{url}?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        var filtered = await admin.GetJsonAsync<CursorPage<ProductSummaryDto>>($"{url}?query=APPLE");
        var viaBarcode = await admin.GetJsonAsync<ProductDto>($"{url}/by-barcode/4006381333931");
        using var badCursor = await admin.GetAsync($"{url}?cursor=garbage");

        Assert.Equal(["Apple", "Apple juice"], first.Items.Select(p => p.Name));
        Assert.Equal(["Bread", "Cheese"], second.Items.Select(p => p.Name));
        Assert.Null(second.NextCursor);
        Assert.Equal(["Apple", "Apple juice"], filtered.Items.Select(p => p.Name));
        Assert.Equal("Bread", viaBarcode.Name);
        await TestClient.AssertProblemAsync(badCursor, HttpStatusCode.BadRequest, ErrorCodes.CursorInvalid);
    }

    [Fact]
    public async Task RestrictedMember_MayRead_ButNotChange()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var product = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct());
        var (kid, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = kid;
        var url = CatalogApi.Products(setup.HouseholdId);

        using var list = await kid.GetAsync(url);
        using var categories = await kid.GetAsync($"/api/v1/households/{setup.HouseholdId}/catalog/categories");
        using var create = await kid.PostAsync(url, CatalogApi.NewProduct("Other"));
        using var update = await CatalogApi.SendAsync(kid, HttpMethod.Put, $"{url}/{product.Id}", CatalogApi.Update("x"), CatalogApi.ETag(product.Version));
        using var archive = await CatalogApi.DeleteAsync(kid, $"{url}/{product.Id}", CatalogApi.ETag(product.Version));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        await TestClient.AssertProblemAsync(create, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(update, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(archive, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignHousehold_Returns404_AndProductsDoNotLeakAcrossHouseholds()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var product = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Milk", "4006381333931"));
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;

        using var list = await outsider.GetAsync(CatalogApi.Products(setup.HouseholdId));
        using var create = await outsider.PostAsync(CatalogApi.Products(setup.HouseholdId), CatalogApi.NewProduct("Other"));
        using var locations = await outsider.GetAsync(CatalogApi.Locations(setup.HouseholdId));
        // Own household, foreign product id: not found, and the barcode is free there.
        using var otherId = await outsider.GetAsync($"{CatalogApi.Products(foreign)}/{product.Id}");
        using var updateOther = await CatalogApi.SendAsync(outsider, HttpMethod.Put, $"{CatalogApi.Products(foreign)}/{product.Id}", CatalogApi.Update("x"), CatalogApi.ETag(product.Version));
        using var byBarcode = await outsider.GetAsync($"{CatalogApi.Products(foreign)}/by-barcode/4006381333931");
        var own = await CatalogApi.CreateProductAsync(outsider, foreign, CatalogApi.NewProduct("Milk", "4006381333931"));

        await TestClient.AssertProblemAsync(list, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(create, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(locations, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(otherId, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(updateOther, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(byBarcode, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        Assert.NotEqual(product.Id, own.Id);
    }

    [Fact]
    public async Task Categories_ListsTheFixedCategories()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;

        var categories = await admin.GetJsonAsync<List<CategoryDto>>($"/api/v1/households/{setup.HouseholdId}/catalog/categories");

        Assert.Equal(FoodAssistant.Domain.Catalog.ProductCategories.All, categories.Select(c => c.Key));
    }
}

using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Catalog;

public sealed class StorageLocationEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Crud_CreatesListsRenamesAndDeletes()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var url = CatalogApi.Locations(setup.HouseholdId);

        var fridge = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location("  Fridge "));
        var freezer = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location("Freezer", StorageTypeDto.Frozen));
        using var rename = await CatalogApi.SendAsync(admin, HttpMethod.Put, $"{url}/{fridge.Id}", CatalogApi.Location("Big fridge"), CatalogApi.ETag(fridge.Version));
        var renamed = await TestClient.ReadAsync<StorageLocationDto>(rename);
        using var delete = await CatalogApi.DeleteAsync(admin, $"{url}/{freezer.Id}", CatalogApi.ETag(freezer.Version));
        var list = await admin.GetJsonAsync<List<StorageLocationDto>>(url);

        Assert.Equal("Fridge", fridge.Name);
        Assert.Equal(StorageTypeDto.Frozen, freezer.Type);
        Assert.True(freezer.SortOrder > fridge.SortOrder);
        Assert.Equal("Big fridge", renamed.Name);
        Assert.Equal(CatalogApi.ETag(renamed.Version), rename.Headers.ETag?.Tag);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal([fridge.Id], list.Select(l => l.Id));
    }

    [Fact]
    public async Task Create_Invalid_ReturnsValidationProblem()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;

        using var response = await admin.PostAsync(CatalogApi.Locations(setup.HouseholdId), CatalogApi.Location(" "));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, CatalogErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Update_NeedsIfMatch_AndRejectsStaleVersion()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var location = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location());
        var url = $"{CatalogApi.Locations(setup.HouseholdId)}/{location.Id}";

        using var missing = await admin.PutAsync(url, CatalogApi.Location("x"));
        using var stale = await CatalogApi.SendAsync(admin, HttpMethod.Put, url, CatalogApi.Location("x"), CatalogApi.ETag(location.Version + 1));
        using var deleteMissing = await admin.DeleteAsync(url);

        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        await TestClient.AssertProblemAsync(deleteMissing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
    }

    [Fact]
    public async Task WithBatches_TypeChangeAndDeleteAreRefused()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var location = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location());
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await HouseholdDataSeed.BatchAsync(app.Services, setup.HouseholdId, product.Id, location.Id);
        var url = $"{CatalogApi.Locations(setup.HouseholdId)}/{location.Id}";

        using var typeChange = await CatalogApi.SendAsync(admin, HttpMethod.Put, url, CatalogApi.Location("Fridge", StorageTypeDto.Frozen), CatalogApi.ETag(location.Version));
        using var delete = await CatalogApi.DeleteAsync(admin, url, CatalogApi.ETag(location.Version));

        await TestClient.AssertProblemAsync(typeChange, HttpStatusCode.Conflict, CatalogErrorCodes.LocationTypeLocked);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Conflict, CatalogErrorCodes.LocationInUse);
    }

    [Fact]
    public async Task Delete_ClearsTheDefaultLocationOfProducts()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var location = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location());
        var product = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct() with { DefaultStorageLocationId = location.Id });

        using var delete = await CatalogApi.DeleteAsync(admin, $"{CatalogApi.Locations(setup.HouseholdId)}/{location.Id}", CatalogApi.ETag(location.Version));
        var after = await admin.GetJsonAsync<ProductDto>($"{CatalogApi.Products(setup.HouseholdId)}/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(location.Id, product.DefaultStorageLocationId);
        Assert.Null(after.DefaultStorageLocationId);
    }

    [Fact]
    public async Task RestrictedMember_MayList_ButNotChange()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var location = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location());
        var (kid, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = kid;
        var url = CatalogApi.Locations(setup.HouseholdId);

        using var list = await kid.GetAsync(url);
        using var create = await kid.PostAsync(url, CatalogApi.Location("Pantry", StorageTypeDto.Ambient));
        using var update = await CatalogApi.SendAsync(kid, HttpMethod.Put, $"{url}/{location.Id}", CatalogApi.Location("x"), CatalogApi.ETag(location.Version));
        using var delete = await CatalogApi.DeleteAsync(kid, $"{url}/{location.Id}", CatalogApi.ETag(location.Version));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        await TestClient.AssertProblemAsync(create, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(update, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignHousehold_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var adminScope = admin;
        var location = await CatalogApi.CreateLocationAsync(admin, setup.HouseholdId, CatalogApi.Location());
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;

        using var list = await outsider.GetAsync(CatalogApi.Locations(setup.HouseholdId));
        using var create = await outsider.PostAsync(CatalogApi.Locations(setup.HouseholdId), CatalogApi.Location("x"));
        // Own household, foreign location id.
        using var update = await CatalogApi.SendAsync(outsider, HttpMethod.Put, $"{CatalogApi.Locations(foreign)}/{location.Id}", CatalogApi.Location("x"), CatalogApi.ETag(location.Version));
        using var delete = await CatalogApi.DeleteAsync(outsider, $"{CatalogApi.Locations(foreign)}/{location.Id}", CatalogApi.ETag(location.Version));
        var own = await outsider.GetJsonAsync<List<StorageLocationDto>>(CatalogApi.Locations(foreign));

        await TestClient.AssertProblemAsync(list, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(create, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(update, HttpStatusCode.NotFound, CatalogErrorCodes.LocationNotFound);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.NotFound, CatalogErrorCodes.LocationNotFound);
        Assert.Empty(own);
    }
}

using FoodAssistant.Application.Catalog;
using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Catalog;

public sealed class CatalogStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(TestDatabase Database, Guid HouseholdA, Guid HouseholdB)> SetupAsync()
    {
        var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var a = (await Seed.HouseholdAsync(scope.Db, user.Id, "A")).Id;
        var b = (await Seed.HouseholdAsync(scope.Db, user.Id, "B")).Id;
        return (database, a, b);
    }

    private static NewProduct NewProduct(Guid household, string name, params string[] barcodes) =>
        new(Guid.CreateVersion7(), household, name, null, ProductCategories.Dairy, Unit.Litre, null, null, null, barcodes, null, Now);

    private static ProductChanges Changes(string name, IReadOnlyList<string> barcodes, Quantity? minimum = null, Guid? location = null) =>
        new(name, null, ProductCategories.Dairy, Unit.Litre, location, null, minimum, barcodes, Now);

    [Fact]
    public async Task Search_FindsByNameFragmentCaseInsensitive_AndByExactBarcode_NotArchived()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        await store.AddProductAsync(NewProduct(a, "Whole Milk", "4006381333931"), Ct);
        await store.AddProductAsync(NewProduct(a, "Oat drink"), Ct);
        var gone = (await store.AddProductAsync(NewProduct(a, "Old milk"), Ct)).Value!;
        await store.ArchiveProductAsync(gone.Id, null, Now, Ct);

        var byName = await store.SearchAsync("MILK", null, 10, Ct);
        var byBarcode = await store.SearchAsync("4006381333931", null, 10, Ct);
        var all = await store.SearchAsync(null, null, 10, Ct);

        Assert.Equal(["Whole Milk"], byName.Select(p => p.Name));
        Assert.Equal(["Whole Milk"], byBarcode.Select(p => p.Name));
        Assert.Equal(["Oat drink", "Whole Milk"], all.Select(p => p.Name));
    }

    [Fact]
    public async Task Search_TreatsLikeWildcardsLiterally()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        await store.AddProductAsync(NewProduct(a, "100% juice"), Ct);
        await store.AddProductAsync(NewProduct(a, "Plain juice"), Ct);

        var found = await store.SearchAsync("%", null, 10, Ct);

        Assert.Equal(["100% juice"], found.Select(p => p.Name));
    }

    [Fact]
    public async Task Search_Cursor_PagesByNameThenId_WithoutGapsOrRepeats()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        foreach (var name in new[] { "Bread", "Apple", "Cheese", "Apple", "Dates" })
        {
            await store.AddProductAsync(NewProduct(a, name), Ct);
        }

        var seen = new List<Guid>();
        ProductCursor? cursor = null;
        while (true)
        {
            var page = await store.SearchAsync(null, cursor, 2, Ct);
            seen.AddRange(page.Select(p => p.Id));
            if (page.Count < 2)
            {
                break;
            }

            cursor = new ProductCursor(page[^1].Name, page[^1].Id);
        }

        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());
        var names = await store.SearchAsync(null, null, 10, Ct);
        Assert.Equal(seen, names.Select(p => p.Id));
        Assert.Equal(["Apple", "Apple", "Bread", "Cheese", "Dates"], names.Select(p => p.Name));
    }

    [Fact]
    public async Task Barcode_IsUniquePerHousehold_ButMayRepeatAcrossHouseholds()
    {
        var (database, a, b) = await SetupAsync();
        await using var _ = database;
        await using (var scopeA = database.CreateScope(a))
        {
            var store = scopeA.Get<ICatalogStore>();
            Assert.Equal(OperationStatus.Created, (await store.AddProductAsync(NewProduct(a, "Milk", "4006381333931"), Ct)).Status);

            var duplicate = await store.AddProductAsync(NewProduct(a, "Other milk", "4006381333931"), Ct);

            Assert.Equal(OperationStatus.Conflict, duplicate.Status);
            Assert.Equal(CatalogErrorCodes.BarcodeInUse, duplicate.ErrorCode);
        }

        await using var scopeB = database.CreateScope(b);
        var inOther = await scopeB.Get<ICatalogStore>().AddProductAsync(NewProduct(b, "Milk", "4006381333931"), Ct);
        Assert.Equal(OperationStatus.Created, inOther.Status);
    }

    [Fact]
    public async Task Barcode_LookupDoesNotCrossHouseholds()
    {
        var (database, a, b) = await SetupAsync();
        await using var _ = database;
        await using (var scopeA = database.CreateScope(a))
        {
            await scopeA.Get<ICatalogStore>().AddProductAsync(NewProduct(a, "Milk", "4006381333931"), Ct);
        }

        await using var scopeB = database.CreateScope(b);
        Assert.Null(await scopeB.Get<ICatalogStore>().FindByBarcodeAsync("4006381333931", Ct));
    }

    [Fact]
    public async Task Update_ReplacesBarcodes_BumpsUpdatedAt_AndChecksVersion()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        var created = (await store.AddProductAsync(NewProduct(a, "Milk", "4006381333931", "12345678"), Ct)).Value!;

        var stale = await store.UpdateProductAsync(created.Id, created.Version + 1, Changes("Milk", []), Ct);
        var ok = await store.UpdateProductAsync(created.Id, created.Version, Changes("Whole milk", ["12345678", "87654321"]), Ct);

        Assert.Equal(OperationStatus.VersionMismatch, stale.Status);
        Assert.Equal(created.Version, stale.CurrentVersion);
        Assert.Equal(OperationStatus.Ok, ok.Status);
        Assert.Equal(["12345678", "87654321"], ok.Value!.Barcodes);
        Assert.NotEqual(created.Version, ok.Value.Version);
        Assert.Equal(Now, (await scope.Db.Set<FoodAssistant.Infrastructure.Catalog.Product>().AsNoTracking().SingleAsync(Ct)).UpdatedAt);
    }

    [Fact]
    public async Task Update_MinimumStockChange_ResetsTriggeredFlag()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        var created = (await store.AddProductAsync(NewProduct(a, "Milk"), Ct)).Value!;
        var first = await store.UpdateProductAsync(created.Id, null, Changes("Milk", [], new Quantity(2m, Unit.Litre)), Ct);
        var product = await scope.Db.Set<FoodAssistant.Infrastructure.Catalog.Product>().SingleAsync(Ct);
        product.MinimumStockTriggered = true;
        await scope.Db.SaveChangesAsync(Ct);

        await store.UpdateProductAsync(created.Id, null, Changes("Milk renamed", [], new Quantity(2m, Unit.Litre)), Ct);
        Assert.True((await scope.Db.Set<FoodAssistant.Infrastructure.Catalog.Product>().AsNoTracking().SingleAsync(Ct)).MinimumStockTriggered);

        await store.UpdateProductAsync(created.Id, null, Changes("Milk renamed", [], new Quantity(3m, Unit.Litre)), Ct);
        Assert.False((await scope.Db.Set<FoodAssistant.Infrastructure.Catalog.Product>().AsNoTracking().SingleAsync(Ct)).MinimumStockTriggered);
        Assert.Equal(OperationStatus.Ok, first.Status);
    }

    [Fact]
    public async Task Archive_WithBatch_IsRefused_WithoutBatch_FreesBarcodes()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        var location = (await store.AddLocationAsync(new NewStorageLocation(Guid.CreateVersion7(), a, "Fridge", StorageType.Chilled, null, Now), Ct)).Value!;
        var stocked = (await store.AddProductAsync(NewProduct(a, "Milk", "4006381333931"), Ct)).Value!;
        var empty = (await store.AddProductAsync(NewProduct(a, "Butter", "12345678"), Ct)).Value!;
        await M1Seed.BatchAsync(scope.Db, a, stocked.Id, location.Id);

        var refused = await store.ArchiveProductAsync(stocked.Id, null, Now, Ct);
        var archived = await store.ArchiveProductAsync(empty.Id, null, Now, Ct);
        var reused = await store.AddProductAsync(NewProduct(a, "New butter", "12345678"), Ct);

        Assert.Equal(CatalogErrorCodes.ProductHasStock, refused.ErrorCode);
        Assert.Equal(OperationStatus.Deleted, archived.Status);
        Assert.Equal(OperationStatus.Created, reused.Status);
        Assert.Null(await store.GetProductAsync(empty.Id, Ct));
        Assert.Equal(OperationStatus.NotFound, (await store.ArchiveProductAsync(empty.Id, null, Now, Ct)).Status);
    }

    [Fact]
    public async Task Location_DeleteClearsDefaultOfProducts_AndIsRefusedWithBatches()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        var shelf = (await store.AddLocationAsync(new NewStorageLocation(Guid.CreateVersion7(), a, "Shelf", StorageType.Ambient, null, Now), Ct)).Value!;
        var fridge = (await store.AddLocationAsync(new NewStorageLocation(Guid.CreateVersion7(), a, "Fridge", StorageType.Chilled, null, Now), Ct)).Value!;
        var product = (await store.AddProductAsync(NewProduct(a, "Rice") with { DefaultStorageLocationId = shelf.Id }, Ct)).Value!;
        await M1Seed.BatchAsync(scope.Db, a, product.Id, fridge.Id);

        var inUse = await store.DeleteLocationAsync(fridge.Id, null, Now, Ct);
        var deleted = await store.DeleteLocationAsync(shelf.Id, shelf.Version, Now, Ct);

        Assert.Equal(CatalogErrorCodes.LocationInUse, inUse.ErrorCode);
        Assert.Equal(OperationStatus.Deleted, deleted.Status);
        Assert.Null((await store.GetProductAsync(product.Id, Ct))!.DefaultStorageLocationId);
        Assert.Equal([fridge.Id], (await store.ListLocationsAsync(Ct)).Select(l => l.Id));
    }

    [Fact]
    public async Task Location_TypeIsLockedWhileBatchesExist_ButNameMayChange()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();
        var fridge = (await store.AddLocationAsync(new NewStorageLocation(Guid.CreateVersion7(), a, "Fridge", StorageType.Chilled, null, Now), Ct)).Value!;
        var product = (await store.AddProductAsync(NewProduct(a, "Milk"), Ct)).Value!;
        await M1Seed.BatchAsync(scope.Db, a, product.Id, fridge.Id);

        var locked = await store.UpdateLocationAsync(fridge.Id, null, new StorageLocationChanges("Fridge", StorageType.Frozen, null, Now), Ct);
        var renamed = await store.UpdateLocationAsync(fridge.Id, null, new StorageLocationChanges("Big fridge", StorageType.Chilled, null, Now), Ct);

        Assert.Equal(CatalogErrorCodes.LocationTypeLocked, locked.ErrorCode);
        Assert.Equal("Big fridge", renamed.Value!.Name);
    }

    [Fact]
    public async Task Location_NewOnesAppendToTheEnd()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);
        var store = scope.Get<ICatalogStore>();

        foreach (var name in new[] { "One", "Two", "Three" })
        {
            await store.AddLocationAsync(new NewStorageLocation(Guid.CreateVersion7(), a, name, StorageType.Ambient, null, Now), Ct);
        }

        Assert.Equal(["One", "Two", "Three"], (await store.ListLocationsAsync(Ct)).Select(l => l.Name));
    }

    [Fact]
    public async Task Product_UnknownDefaultLocation_IsInvalid()
    {
        var (database, a, _) = await SetupAsync();
        await using var _ = database;
        await using var scope = database.CreateScope(a);

        var result = await scope.Get<ICatalogStore>().AddProductAsync(NewProduct(a, "Milk") with { DefaultStorageLocationId = Guid.CreateVersion7() }, Ct);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Contains("defaultStorageLocationId", result.Errors!.Keys);
    }
}

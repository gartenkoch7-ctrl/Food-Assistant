using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.ShelfLife;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.ShelfLife;

public sealed class ShelfLifeOverrideStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CategoryOverride_UpsertReadsBackAndReplaces()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _, _) = await SeedAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var store = scope.Get<IShelfLifeOverrideStore>();

        await store.UpsertCategoryAsync(a, ProductCategories.Dairy, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = 3 }, Now, Token);
        await store.UpsertCategoryAsync(a, ProductCategories.Dairy, StorageType.Chilled, new ShelfLifeRule { DaysAsLeftover = 2 }, Now.AddHours(1), Token);

        Assert.Equal(new ShelfLifeRule { DaysAsLeftover = 2 }, await store.GetForCategoryAsync(ProductCategories.Dairy, StorageType.Chilled, Token));
        Assert.Null(await store.GetForCategoryAsync(ProductCategories.Dairy, StorageType.Frozen, Token));
        var row = await scope.Db.Set<ShelfLifeOverride>().AsNoTracking().SingleAsync(Token);
        Assert.Equal((Now, Now.AddHours(1)), (row.CreatedAt, row.UpdatedAt));
        var listed = Assert.Single(await store.ListCategoryOverridesAsync(Token));
        Assert.Equal((ProductCategories.Dairy, StorageType.Chilled), (listed.CategoryKey, listed.StorageType));
    }

    [Fact]
    public async Task ProductOverride_UpsertListAndDelete()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, milk, _) = await SeedAsync(database);
        await using var scope = database.CreateScope(activeHousehold: a);
        var store = scope.Get<IShelfLifeOverrideStore>();

        Assert.True(await store.UpsertProductAsync(a, milk, StorageType.Frozen, new ShelfLifeRule { DaysFrozen = 30 }, Now, Token));
        Assert.True(await store.UpsertProductAsync(a, milk, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = 4 }, Now, Token));

        Assert.Equal(new ShelfLifeRule { DaysFrozen = 30 }, await store.GetForProductAsync(milk, StorageType.Frozen, Token));
        var all = await store.ListProductOverridesAsync(milk, Token);
        Assert.Equal([StorageType.Chilled, StorageType.Frozen], all.Keys.Order());
        Assert.Empty(await store.ListCategoryOverridesAsync(Token));

        await store.DeleteProductAsync(milk, StorageType.Frozen, Token);
        await store.DeleteProductAsync(milk, StorageType.Frozen, Token);

        Assert.Null(await store.GetForProductAsync(milk, StorageType.Frozen, Token));
        Assert.Single(await store.ListProductOverridesAsync(milk, Token));
    }

    [Fact]
    public async Task ForeignOrArchivedProduct_IsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _, foreignMilk) = await SeedAsync(database);
        Guid archived;
        await using (var seed = database.CreateScope())
        {
            var product = await M1Seed.ProductAsync(seed.Db, a, "Old");
            product.ArchivedAt = Now;
            await seed.Db.SaveChangesAsync(Token);
            archived = product.Id;
        }

        await using var scope = database.CreateScope(activeHousehold: a);
        var store = scope.Get<IShelfLifeOverrideStore>();

        Assert.Null(await store.GetCategoryOfProductAsync(foreignMilk, Token));
        Assert.Null(await store.GetCategoryOfProductAsync(archived, Token));
        Assert.False(await store.UpsertProductAsync(a, foreignMilk, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = 1 }, Now, Token));
        Assert.False(await store.UpsertProductAsync(a, archived, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = 1 }, Now, Token));
        Assert.Empty(await scope.Db.Set<ShelfLifeOverride>().IgnoreQueryFilters().ToListAsync(Token));
    }

    [Fact]
    public async Task Overrides_AreSeparatedByHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, milk, foreignMilk) = await SeedAsync(database);
        var b = await HouseholdOfAsync(database, foreignMilk);
        await using (var scopeB = database.CreateScope(activeHousehold: b))
        {
            var storeB = scopeB.Get<IShelfLifeOverrideStore>();
            await storeB.UpsertCategoryAsync(b, ProductCategories.Dairy, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = 9 }, Now, Token);
            Assert.Equal(ProductCategories.Dairy, await storeB.GetCategoryOfProductAsync(foreignMilk, Token));
        }

        await using var scope = database.CreateScope(activeHousehold: a);
        var store = scope.Get<IShelfLifeOverrideStore>();
        await store.DeleteCategoryAsync(ProductCategories.Dairy, StorageType.Chilled, Token);

        Assert.Null(await store.GetForCategoryAsync(ProductCategories.Dairy, StorageType.Chilled, Token));
        Assert.Empty(await store.ListCategoryOverridesAsync(Token));
        Assert.Equal(ProductCategories.Dairy, await store.GetCategoryOfProductAsync(milk, Token));
        Assert.Equal(1, await scope.Db.Set<ShelfLifeOverride>().IgnoreQueryFilters().CountAsync(Token));
    }

    [Fact]
    public async Task ConcurrentFirstUpserts_LastWriteWinsWithoutError()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var (a, _, _) = await SeedAsync(database);

        var writes = Enumerable.Range(1, 8).Select(async days =>
        {
            await using var scope = database.CreateScope(activeHousehold: a);
            await scope.Get<IShelfLifeOverrideStore>().UpsertCategoryAsync(
                a, ProductCategories.Fish, StorageType.Chilled, new ShelfLifeRule { DaysAfterOpening = days }, Now, Token);
        });
        await Task.WhenAll(writes);

        await using var check = database.CreateScope(activeHousehold: a);
        Assert.Single(await check.Get<IShelfLifeOverrideStore>().ListCategoryOverridesAsync(Token));
    }

    private static async Task<(Guid A, Guid Milk, Guid ForeignMilk)> SeedAsync(TestDatabase database)
    {
        await using var scope = database.CreateScope();
        var user = await Seed.UserAsync(scope.Db, "owner");
        var a = await Seed.HouseholdAsync(scope.Db, user.Id, "A");
        var b = await Seed.HouseholdAsync(scope.Db, user.Id, "B");
        var milk = await M1Seed.ProductAsync(scope.Db, a.Id);
        var foreignMilk = await M1Seed.ProductAsync(scope.Db, b.Id);
        return (a.Id, milk.Id, foreignMilk.Id);
    }

    private static async Task<Guid> HouseholdOfAsync(TestDatabase database, Guid productId)
    {
        await using var scope = database.CreateScope();

        // Test lookup across households on purpose: the test needs the other household's id.
        return await scope.Db.Set<Catalog.Product>().IgnoreQueryFilters()
            .Where(p => p.Id == productId)
            .Select(p => p.HouseholdId)
            .SingleAsync(Token);
    }
}

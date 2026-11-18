using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Infrastructure.FoodFacts;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.FoodFacts;

public sealed class FoodFactsCacheStoreTests(PostgresFixture postgres)
{
    private const string Barcode = "4000000000017";
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly FoodFactsProduct Milk = new(
        Barcode, "Testmilch", "Testmilch DE", null, "Testmarke", "1 l", 1000, "ml", ["en:milks"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_ReturnsNull()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();

        Assert.Null(await scope.Get<IFoodFactsCache>().GetAsync(Barcode, Start, Ct));
    }

    [Fact]
    public async Task Found_RoundTripsProduct_AndTouchesLastRequested()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var cache = scope.Get<IFoodFactsCache>();
        await cache.StoreAsync(Barcode, new FoodFactsLookup(FoodFactsStatus.Found, Milk), Start, Start.AddDays(30), Ct);

        var hit = await cache.GetAsync(Barcode, Start.AddDays(3), Ct);

        Assert.NotNull(hit);
        Assert.Equal(FoodFactsStatus.Found, hit.Lookup.Status);
        var product = hit.Lookup.Product!;
        Assert.Equal((Milk.Barcode, Milk.Name, Milk.NameDe, Milk.Brand), (product.Barcode, product.Name, product.NameDe, product.Brand));
        Assert.Equal((Milk.QuantityText, Milk.PackageQuantity, Milk.PackageUnit), (product.QuantityText, product.PackageQuantity, product.PackageUnit));
        Assert.Equal(["en:milks"], product.CategoryTags);
        Assert.Equal(Start.AddDays(30), hit.ExpiresAt);
        var entry = await scope.Db.Set<FoodFactsCacheEntry>().SingleAsync(Ct);
        Assert.Equal(Start.AddDays(3), entry.LastRequestedAt);
    }

    [Fact]
    public async Task NotFound_IsStored_AndStoreReplacesExisting()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var cache = scope.Get<IFoodFactsCache>();
        await cache.StoreAsync(Barcode, new FoodFactsLookup(FoodFactsStatus.Found, Milk), Start, Start.AddDays(30), Ct);

        await cache.StoreAsync(Barcode, FoodFactsLookup.NotFound, Start.AddDays(31), Start.AddDays(33), Ct);
        var hit = await cache.GetAsync(Barcode, Start.AddDays(32), Ct);

        Assert.Equal(FoodFactsStatus.NotFound, hit!.Lookup.Status);
        Assert.Null(hit.Lookup.Product);
        Assert.Equal(Start.AddDays(33), hit.ExpiresAt);
        Assert.Equal(1, await scope.Db.Set<FoodFactsCacheEntry>().CountAsync(Ct));
    }

    [Fact]
    public async Task Store_DropsEntriesNobodyRequestedFor90Days()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var cache = scope.Get<IFoodFactsCache>();
        await cache.StoreAsync("11111111", FoodFactsLookup.NotFound, Start, Start.AddDays(2), Ct);

        await cache.StoreAsync(Barcode, FoodFactsLookup.NotFound, Start.AddDays(91), Start.AddDays(93), Ct);

        Assert.Equal([Barcode], await scope.Db.Set<FoodFactsCacheEntry>().Select(e => e.Barcode).ToListAsync(Ct));
    }

    [Fact]
    public async Task Unavailable_IsNotStored()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        await using var scope = database.CreateScope();
        var cache = scope.Get<IFoodFactsCache>();

        await cache.StoreAsync(Barcode, FoodFactsLookup.Unavailable, Start, Start.AddDays(2), Ct);

        Assert.Null(await cache.GetAsync(Barcode, Start, Ct));
    }

    [Fact]
    public async Task Cache_IsServerWide_NotPerHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        Guid first;
        Guid second;
        await using (var seed = database.CreateScope())
        {
            var user = await Seed.UserAsync(seed.Db, "owner");
            first = (await Seed.HouseholdAsync(seed.Db, user.Id, "One")).Id;
            second = (await Seed.HouseholdAsync(seed.Db, user.Id, "Two")).Id;
        }

        await using (var scope = database.CreateScope(first))
        {
            await scope.Get<IFoodFactsCache>()
                .StoreAsync(Barcode, new FoodFactsLookup(FoodFactsStatus.Found, Milk), Start, Start.AddDays(30), Ct);
        }

        await using var other = database.CreateScope(second);
        Assert.NotNull(await other.Get<IFoodFactsCache>().GetAsync(Barcode, Start, Ct));
    }
}

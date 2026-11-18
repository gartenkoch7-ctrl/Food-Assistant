using FoodAssistant.Application.FoodFacts;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Application.Tests.FoodFacts;

public sealed class FoodFactsServiceTests
{
    private const string Barcode = "4000000000017";
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly FoodFactsProduct Milk = new(
        Barcode, "Testmilch", null, null, "Testmarke", "1 l", 1000, "ml", ["en:milks"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Miss_FetchesAndStoresFoundFor30Days()
    {
        var (service, api, cache, _) = Create(new FoodFactsLookup(FoodFactsStatus.Found, Milk));

        var lookup = await service.LookupAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.Found, lookup.Status);
        Assert.Equal(1, api.Calls);
        Assert.Equal(Start.AddDays(30), cache.Stored[Barcode].ExpiresAt);
    }

    [Fact]
    public async Task FreshEntry_IsServedWithoutCall()
    {
        var (service, api, cache, time) = Create(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await service.LookupAsync(Barcode, Ct);
        time.Now = Start.AddDays(29);

        var lookup = await service.LookupAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.Found, lookup.Status);
        Assert.False(lookup.Stale);
        Assert.Equal(1, api.Calls);
        Assert.Equal(time.Now, cache.LastRequested[Barcode]);
    }

    [Fact]
    public async Task NotFound_IsCachedForTwoDays()
    {
        var (service, api, cache, time) = Create(FoodFactsLookup.NotFound);
        await service.LookupAsync(Barcode, Ct);
        Assert.Equal(Start.AddDays(2), cache.Stored[Barcode].ExpiresAt);

        time.Now = Start.AddDays(1);
        Assert.Equal(FoodFactsStatus.NotFound, (await service.LookupAsync(Barcode, Ct)).Status);
        Assert.Equal(1, api.Calls);

        time.Now = Start.AddDays(3);
        await service.LookupAsync(Barcode, Ct);
        Assert.Equal(2, api.Calls);
    }

    [Fact]
    public async Task ExpiredEntry_AndFailedRefresh_ServesStale()
    {
        var (service, api, _, time) = Create(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await service.LookupAsync(Barcode, Ct);
        api.Next = FoodFactsLookup.Unavailable;
        time.Now = Start.AddDays(31);

        var lookup = await service.LookupAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.Found, lookup.Status);
        Assert.True(lookup.Stale);
        Assert.Equal(Milk, lookup.Product);
    }

    [Fact]
    public async Task NothingCached_AndUnavailable_IsNotStored()
    {
        var (service, _, cache, _) = Create(FoodFactsLookup.Unavailable);

        var lookup = await service.LookupAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.Unavailable, lookup.Status);
        Assert.Empty(cache.Stored);
    }

    [Fact]
    public async Task ExpiredEntry_AndSuccessfulRefresh_ReplacesIt()
    {
        var (service, api, cache, time) = Create(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await service.LookupAsync(Barcode, Ct);
        api.Next = FoodFactsLookup.NotFound;
        time.Now = Start.AddDays(31);

        var lookup = await service.LookupAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.NotFound, lookup.Status);
        Assert.Equal(Start.AddDays(33), cache.Stored[Barcode].ExpiresAt);
    }

    private static (FoodFactsService Service, FakeApi Api, FakeCache Cache, FakeTime Time) Create(FoodFactsLookup answer)
    {
        var api = new FakeApi { Next = answer };
        var cache = new FakeCache();
        var time = new FakeTime { Now = Start };
        return (new FoodFactsService(api, cache, time, NullLogger<FoodFactsService>.Instance), api, cache, time);
    }

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeApi : IFoodFactsApi
    {
        public FoodFactsLookup Next { get; set; } = FoodFactsLookup.Unavailable;

        public int Calls { get; private set; }

        public Task<FoodFactsLookup> FetchAsync(string barcode, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Next);
        }
    }

    private sealed class FakeCache : IFoodFactsCache
    {
        public Dictionary<string, FoodFactsCacheHit> Stored { get; } = [];

        public Dictionary<string, DateTimeOffset> LastRequested { get; } = [];

        public Task<FoodFactsCacheHit?> GetAsync(string barcode, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (!Stored.TryGetValue(barcode, out var hit))
            {
                return Task.FromResult<FoodFactsCacheHit?>(null);
            }

            LastRequested[barcode] = now;
            return Task.FromResult<FoodFactsCacheHit?>(hit);
        }

        public Task StoreAsync(
            string barcode,
            FoodFactsLookup lookup,
            DateTimeOffset fetchedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Stored[barcode] = new FoodFactsCacheHit(lookup, expiresAt);
            return Task.CompletedTask;
        }
    }
}

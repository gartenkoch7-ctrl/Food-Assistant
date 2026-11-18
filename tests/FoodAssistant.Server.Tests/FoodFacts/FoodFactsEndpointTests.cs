using System.Net;
using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Contracts.FoodFacts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.FoodFacts;

/// <summary>Barcode lookup endpoint with a fake Open Food Facts API; the real service is never called.</summary>
public sealed class FoodFactsEndpointTests(PostgresContainerFixture postgres)
{
    private const string Barcode = "4000000000017";

    private static readonly FoodFactsProduct Milk = new(
        Barcode, "Testmilch", "Testmilch DE", "Test milk", "Testmarke", "1 l", 1000, "ml", ["en:dairies", "en:milks"]);

    [Fact]
    public async Task Found_ReturnsSuggestion_AndCaches()
    {
        var api = new FakeApi(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await using var app = NewApp(api);
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        var dto = await admin.GetJsonAsync<FoodFactsProductDto>(Url(setup.HouseholdId, Barcode));
        var again = await admin.GetJsonAsync<FoodFactsProductDto>(Url(setup.HouseholdId, Barcode));

        Assert.Equal(Barcode, dto.Barcode);
        Assert.Equal("dairy", dto.SuggestedCategoryKey);
        Assert.Equal(("Testmarke", "1 l"), (dto.Brand, dto.QuantityText));
        Assert.Equal(("ml", 1000m), (dto.PackageQuantity?.Unit, dto.PackageQuantity?.Value));
        Assert.False(dto.Stale);
        Assert.Equal(dto, again);
        Assert.Equal(1, api.Calls);
    }

    [Fact]
    public async Task NotFound_Returns404WithCode()
    {
        await using var app = NewApp(new FakeApi(FoodFactsLookup.NotFound));
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        using var response = await admin.GetAsync(Url(setup.HouseholdId, Barcode));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, FoodFactsErrorCodes.NotFound);
    }

    [Fact]
    public async Task Unavailable_Returns503WithCode()
    {
        await using var app = NewApp(new FakeApi(FoodFactsLookup.Unavailable));
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        using var response = await admin.GetAsync(Url(setup.HouseholdId, Barcode));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, FoodFactsErrorCodes.Unavailable);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("40000000000171234")]
    [InlineData("abcdefgh")]
    [InlineData("4000000000O17")]
    public async Task InvalidBarcode_Returns400_WithoutCall(string barcode)
    {
        var api = new FakeApi(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await using var app = NewApp(api);
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        using var response = await admin.GetAsync(Url(setup.HouseholdId, barcode));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, FoodFactsErrorCodes.InvalidBarcode);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task WithoutSignIn_Returns401()
    {
        await using var app = NewApp(new FakeApi(FoodFactsLookup.NotFound));
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        using var anonymous = app.CreateTestClient();

        using var response = await anonymous.GetAsync(Url(setup.HouseholdId, Barcode));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForeignHousehold_Returns404_WithoutCall()
    {
        var api = new FakeApi(new FoodFactsLookup(FoodFactsStatus.Found, Milk));
        await using var app = NewApp(api);
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (member, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "guest");
        using var __ = member;

        using var response = await member.GetAsync(Url(foreign, Barcode));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task Restricted_MayLookUp()
    {
        await using var app = NewApp(new FakeApi(new FoodFactsLookup(FoodFactsStatus.Found, Milk)));
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (restricted, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = restricted;

        using var response = await restricted.GetAsync(Url(setup.HouseholdId, Barcode));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Url(Guid householdId, string barcode) => $"/api/v1/households/{householdId}/food-facts/{barcode}";

    private ServerTestApp NewApp(FakeApi api) => new(
        postgres.NewDatabaseConnectionString(),
        configureServices: services =>
        {
            services.AddSingleton<IFoodFactsApi>(api);
        });

    private sealed class FakeApi(FoodFactsLookup answer) : IFoodFactsApi
    {
        private int _calls;

        public int Calls => _calls;

        public Task<FoodFactsLookup> FetchAsync(string barcode, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(answer);
        }
    }
}

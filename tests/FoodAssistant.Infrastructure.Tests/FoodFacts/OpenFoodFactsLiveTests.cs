using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Infrastructure.FoodFacts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Infrastructure.Tests.FoodFacts;

/// <summary>
/// One real call to Open Food Facts. Never part of normal runs: explicit test and only with
/// <c>FOODASSISTANT_LIVE_TESTS=1</c>, so CI and local runs stay offline and polite to the service.
/// </summary>
public sealed class OpenFoodFactsLiveTests
{
    [Fact(Explicit = true)]
    public async Task KnownBarcode_ReturnsProduct()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("FOODASSISTANT_LIVE_TESTS") == "1",
            "Set FOODASSISTANT_LIVE_TESTS=1 to call the real Open Food Facts API.");
        var options = Options.Create(new OpenFoodFactsOptions());
        using var throttle = new OpenFoodFactsThrottle(options);
        using var http = new HttpClient();
        var api = new OpenFoodFactsApi(http, options, throttle, NullLogger<OpenFoodFactsApi>.Instance);

        // 3017620422003 is a widely known product that Open Food Facts documents in its API examples.
        var lookup = await api.FetchAsync("3017620422003", TestContext.Current.CancellationToken);

        Assert.Equal(FoodFactsStatus.Found, lookup.Status);
        Assert.False(string.IsNullOrWhiteSpace(lookup.Product?.Name));
    }
}

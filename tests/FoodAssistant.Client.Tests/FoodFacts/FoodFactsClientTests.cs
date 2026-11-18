using System.Net;
using FoodAssistant.Client.FoodFacts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.FoodFacts;

namespace FoodAssistant.Client.Tests.FoodFacts;

public sealed class FoodFactsClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (FoodFactsClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new FoodFactsClient(api), handler);
    }

    [Fact]
    public async Task Lookup_GetsTheSuggestion_FromTheBarcodeRoute()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK,
            """{"barcode":"4006381333931","name":"Milk","brand":"Farm","quantityText":"1 l","packageQuantity":{"value":1,"unit":"l"},"suggestedCategoryKey":"dairy","stale":false}""");

        var result = await client.LookupAsync(Household, "4006381333931", Ct);

        Assert.Equal($"https://food.test/api/v1/households/{Household:D}/food-facts/4006381333931", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(("Milk", "dairy", "l"), (result.Value!.Name, result.Value.SuggestedCategoryKey, result.Value.PackageQuantity!.Unit));
    }

    [Fact]
    public async Task Lookup_Unknown_ReturnsTheErrorCode()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.NotFound, """{"status":404,"errorCode":"food_facts.not_found"}""", "application/problem+json");

        var result = await client.LookupAsync(Household, "4006381333931", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(FoodFactsErrorCodes.NotFound, result.Error!.ErrorCode);
    }

    [Fact]
    public async Task Lookup_EscapesTheBarcode()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.BadRequest, """{"status":400,"errorCode":"food_facts.invalid_barcode"}""");

        await client.LookupAsync(Household, "a/b?c", Ct);

        Assert.EndsWith("/food-facts/a%2Fb%3Fc", handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
    }
}

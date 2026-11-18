using System.Net;
using FoodAssistant.Client.Checkout;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Client.Tests.Checkout;

public sealed class CheckoutClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid List = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static readonly Guid Item = Guid.Parse("0197a0c0-0000-7000-8000-000000000003");
    private static string Url => $"https://food.test/api/v1/households/{Household:D}/shopping/lists/{List:D}/checkout";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (CheckoutClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new CheckoutClient(api), handler);
    }

    [Fact]
    public async Task GetProposal_ReadsEntriesWithRuleFlag()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""
            {"listId":"{{List:D}}","entries":[{"itemId":"{{Item:D}}","itemVersion":3,"productId":null,"text":"Brot",
            "categoryKey":null,"quantity":{"value":1,"unit":"piece"},"storageLocationId":null,"expiresOn":"2027-02-16",
            "expiryKind":"use_by","expiryFromRule":true}]}
            """);

        var result = await client.GetProposalAsync(Household, List, Ct);

        Assert.Equal(Url, handler.Requests[0].Uri.AbsoluteUri);
        var entry = Assert.Single(result.Value!.Entries);
        Assert.Equal((Item, 3L, new DateOnly(2027, 2, 16), ExpiryKindDto.UseBy, true), (entry.ItemId, entry.ItemVersion, entry.ExpiresOn!.Value, entry.ExpiryKind, entry.ExpiryFromRule));
        Assert.Equal(new QuantityDto(1m, "piece"), entry.Quantity);
    }

    [Fact]
    public async Task Complete_PostsLines_WithTheGivenIdempotencyKey()
    {
        var (client, handler) = Create();
        var key = Guid.CreateVersion7();
        var result = $$"""{"batchesCreated":0,"itemsRemoved":1,"batchIds":[],"staleItemIds":[]}""";
        handler.EnqueueJson(HttpStatusCode.OK, result).EnqueueJson(HttpStatusCode.OK, result);
        var request = new CompleteCheckoutRequest([new CheckoutLineRequest(Item, 3, true, null, null, null, null, null, ExpiryKindDto.None, false)]);

        var first = await client.CompleteAsync(Household, List, request, key, Ct);
        await client.CompleteAsync(Household, List, request, key, Ct);

        Assert.Equal((0, 1), (first.Value!.BatchesCreated, first.Value.ItemsRemoved));
        Assert.All(handler.Requests, r => Assert.Equal((HttpMethod.Post, Url, key.ToString("D")), (r.Method, r.Uri.AbsoluteUri, r.Headers["Idempotency-Key"])));
        Assert.Contains("\"skip\":true", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"expiryKind\":\"none\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_ValidationFailure_CarriesTheLineFieldErrors()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(
            HttpStatusCode.BadRequest,
            """{"status":400,"errorCode":"checkout.validation_failed","errors":{"lines[0].storageLocationId":["required"]}}""",
            "application/problem+json");

        var result = await client.CompleteAsync(Household, List, new CompleteCheckoutRequest([]), Guid.CreateVersion7(), Ct);

        Assert.Equal("checkout.validation_failed", result.Error!.ErrorCode);
        Assert.Equal(["required"], result.Error.FieldErrors["lines[0].storageLocationId"]);
    }
}

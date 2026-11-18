using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.ShelfLife;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.Client.Tests.ShelfLife;

public sealed class ShelfLifeClientTests
{
    private static readonly Guid Household = Guid.Parse("018f0000-0000-7000-8000-000000000001");
    private static readonly Guid Product = Guid.Parse("018f0000-0000-7000-8000-000000000002");

    private const string RowJson =
        """{"categoryKey":"dairy","storageType":"chilled","default":{"daysAfterOpening":3,"daysFrozen":null,"daysAfterThawing":null,"daysAsLeftover":null},"override":null,"effective":{"daysAfterOpening":3,"daysFrozen":null,"daysAfterThawing":null,"daysAsLeftover":null},"sourceKey":"bzfe"}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (ShelfLifeClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new ShelfLifeClient(api), handler);
    }

    [Fact]
    public async Task ListRules_ReadsRows()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $"[{RowJson}]");

        var result = await client.ListRulesAsync(Household, Ct);

        Assert.Equal($"https://food.test/api/v1/households/{Household}/shelf-life/rules", handler.Requests[0].Uri.ToString());
        var row = Assert.Single(result.Value!);
        Assert.Equal((StorageTypeDto.Chilled, 3, "bzfe"), (row.StorageType, row.Effective.DaysAfterOpening, row.SourceKey));
    }

    [Fact]
    public async Task ListSources_ReadsTheSources()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """[{"key":"bzfe","title":"T","url":"https://www.bzfe.de/","retrieved":"2026-11-18"}]""");

        var result = await client.ListSourcesAsync(Household, Ct);

        Assert.EndsWith("/shelf-life/sources", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal(new DateOnly(2026, 11, 18), Assert.Single(result.Value!).Retrieved);
    }

    [Fact]
    public async Task SetCategoryOverride_PutsTheDaysWithoutIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, RowJson);

        var result = await client.SetCategoryOverrideAsync(Household, "ready_meals", StorageTypeDto.Frozen, new ShelfLifeDaysDto(null, 90, null, null), Ct);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/shelf-life/overrides/categories/ready_meals/frozen", request.Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"daysFrozen\":90", request.Body, StringComparison.Ordinal);
        Assert.False(request.Headers.ContainsKey("If-Match"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CategoryKey_IsEscaped()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        await client.ResetCategoryOverrideAsync(Household, "a/b", StorageTypeDto.Ambient, Ct);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.EndsWith("/categories/a%2Fb/ambient", handler.Requests[0].Uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductCalls_UseTheProductPaths()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""{"productId":"{{Product}}","categoryKey":"dairy","rows":[{{RowJson}}]}""");
        handler.EnqueueJson(HttpStatusCode.OK, RowJson);
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var product = await client.GetProductAsync(Household, Product, Ct);
        await client.SetProductOverrideAsync(Household, Product, StorageTypeDto.Chilled, new ShelfLifeDaysDto(2, null, null, null), Ct);
        var reset = await client.ResetProductOverrideAsync(Household, Product, StorageTypeDto.Chilled, Ct);

        Assert.Equal(Product, product.Value!.ProductId);
        Assert.EndsWith($"/products/{Product}/shelf-life", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.EndsWith($"/products/{Product}/shelf-life/chilled", handler.Requests[1].Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.True(reset.IsSuccess);
    }

    [Fact]
    public async Task ValidationProblem_KeepsTheFieldErrors()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(
            HttpStatusCode.BadRequest,
            """{"errorCode":"shelf_life.validation_failed","errors":{"daysFrozen":["out_of_range"]}}""",
            "application/problem+json");

        var result = await client.SetCategoryOverrideAsync(Household, "dairy", StorageTypeDto.Chilled, new ShelfLifeDaysDto(null, -1, null, null), Ct);

        Assert.Equal(ShelfLifeErrorCodes.ValidationFailed, result.Error!.ErrorCode);
        Assert.Equal(["out_of_range"], result.Error.FieldErrors["daysFrozen"]);
    }
}

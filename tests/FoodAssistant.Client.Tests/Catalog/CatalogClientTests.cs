using System.Net;
using FoodAssistant.Client.Catalog;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Client.Tests.Catalog;

public sealed class CatalogClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid Product = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static string Base => $"https://food.test/api/v1/households/{Household:D}/";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (CatalogClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new CatalogClient(api), handler);
    }

    private const string ProductJson =
        """{"id":"0197a0c0-0000-7000-8000-000000000002","name":"Milk","brand":null,"categoryKey":"dairy","defaultUnit":"l","defaultStorageLocationId":null,"sectionKey":null,"minimumStock":null,"barcodes":["4006381333931"],"fromOpenFoodFacts":false,"version":3}""";

    [Fact]
    public async Task Search_EscapesTheQuery_AndSendsCursorAndLimit()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[],"nextCursor":null}""");

        var result = await client.SearchProductsAsync(Household, "a&b=c/ü", "cur sor", 20, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(Base + "products?query=a%26b%3Dc%2F%C3%BC&cursor=cur%20sor&limit=20", handler.Requests[0].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Search_WithoutParameters_HasNoQueryString()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"items":[{"id":"0197a0c0-0000-7000-8000-000000000002","name":"Milk","brand":null,"categoryKey":"dairy","defaultUnit":"l"}],"nextCursor":"abc"}""");

        var result = await client.SearchProductsAsync(Household, "  ", null, null, Ct);

        Assert.Equal(Base + "products", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("Milk", result.Value!.Items[0].Name);
        Assert.Equal("abc", result.Value.NextCursor);
    }

    [Fact]
    public async Task FindByBarcode_EscapesTheCodeIntoTheRoute()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, ProductJson);

        await client.FindByBarcodeAsync(Household, "../x y", Ct);

        // The slash is escaped, so the code stays one route segment.
        Assert.EndsWith("/products/by-barcode/..%2Fx%20y", handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ReadsTheProductAndItsEtag()
    {
        var (client, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ProductJson, System.Text.Encoding.UTF8, "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"3\"") },
        });

        var result = await client.GetProductAsync(Household, Product, Ct);

        Assert.Equal(Base + $"products/{Product:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(3, result.Value!.Version);
        Assert.Equal("\"3\"", result.ETag);
    }

    [Fact]
    public async Task Update_SendsPutWithIfMatch_AndKeepsFieldErrors()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.BadRequest, """{"errorCode":"catalog.validation_failed","errors":{"name":["required"]}}""", "application/problem+json");

        var result = await client.UpdateProductAsync(
            Household, Product, new UpdateProductRequest("", null, "dairy", "l", null, null, null, null), "\"3\"", Ct);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("\"3\"", request.Headers["If-Match"]);
        Assert.Equal(["required"], result.Error!.FieldErrors["name"]);
    }

    [Fact]
    public async Task Create_PostsWithIdempotencyKey_AndNoIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Created, ProductJson);

        var result = await client.CreateProductAsync(
            Household, new CreateProductRequest(null, "Milk", null, "dairy", "l", null, null, null, null, null), Ct);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.True(request.Headers.ContainsKey("Idempotency-Key"));
        Assert.False(request.Headers.ContainsKey("If-Match"));
        Assert.Equal(Base + "products", request.Uri.AbsoluteUri);
        Assert.Equal("Milk", result.Value!.Name);
    }

    [Fact]
    public async Task Archive_And_DeleteLocation_SendDeleteWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent).EnqueueStatus(HttpStatusCode.NoContent);

        var archived = await client.ArchiveProductAsync(Household, Product, "\"4\"", Ct);
        var deleted = await client.DeleteStorageLocationAsync(Household, Product, "\"5\"", Ct);

        Assert.True(archived.IsSuccess && deleted.IsSuccess);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Delete, r.Method));
        Assert.Equal(["\"4\"", "\"5\""], handler.Requests.Select(r => r.Headers["If-Match"]));
        Assert.EndsWith($"/products/{Product:D}", handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith($"/storage-locations/{Product:D}", handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Locations_ListCreateUpdate_UseTheStorageLocationRoutes()
    {
        var (client, handler) = Create();
        const string location = """{"id":"0197a0c0-0000-7000-8000-000000000002","name":"Fridge","type":"chilled","sortOrder":0,"version":1}""";
        handler.EnqueueJson(HttpStatusCode.OK, $"[{location}]").EnqueueJson(HttpStatusCode.Created, location).EnqueueJson(HttpStatusCode.OK, location);

        var list = await client.ListStorageLocationsAsync(Household, Ct);
        var request = new StorageLocationRequest(null, "Fridge", StorageTypeDto.Chilled, null);
        await client.CreateStorageLocationAsync(Household, request, Ct);
        await client.UpdateStorageLocationAsync(Household, Product, request, "\"1\"", Ct);

        Assert.Equal(StorageTypeDto.Chilled, list.Value![0].Type);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put],
            handler.Requests.Select(r => r.Method));
        Assert.All(handler.Requests, r => Assert.Contains("/storage-locations", r.Uri.AbsoluteUri, StringComparison.Ordinal));
        Assert.Contains("\"type\":\"chilled\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Categories_UseTheCatalogRoute()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """[{"key":"dairy","defaultSectionKey":"dairy_chilled","defaultStorageType":"chilled"}]""");

        var result = await client.ListCategoriesAsync(Household, Ct);

        Assert.Equal(Base + "catalog/categories", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("dairy", result.Value![0].Key);
    }
}

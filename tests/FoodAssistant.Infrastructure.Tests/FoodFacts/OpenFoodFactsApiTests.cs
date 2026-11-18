using System.Net;
using System.Text;
using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Infrastructure.FoodFacts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Infrastructure.Tests.FoodFacts;

/// <summary>Against a fake handler with invented answers; never the real Open Food Facts.</summary>
public sealed class OpenFoodFactsApiTests
{
    private const string Barcode = "4000000000017";

    private const string FoundJson = """
        {"code":"4000000000017","status":1,"product":{"code":"4000000000017","product_name":"Testmilch",
        "product_name_de":"Testmilch 1 l","product_name_en":"","generic_name":"Milch","brands":"Testmarke, Zweitmarke",
        "quantity":"1 l","product_quantity":"1000","product_quantity_unit":"ml",
        "categories_tags":["en:dairies","en:milks"]}}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Found_ParsesReducedProduct()
    {
        var (api, handler) = Create(_ => Json(HttpStatusCode.OK, FoundJson));

        var lookup = await api.FetchAsync(Barcode, Ct);

        Assert.Equal(FoodFactsStatus.Found, lookup.Status);
        var product = lookup.Product!;
        Assert.Equal((Barcode, "Testmilch", "Testmilch 1 l", null), (product.Barcode, product.Name, product.NameDe, product.NameEn));
        Assert.Equal(("Testmarke", "1 l", 1000m, "ml"), (product.Brand, product.QuantityText, product.PackageQuantity, product.PackageUnit));
        Assert.Equal(["en:dairies", "en:milks"], product.CategoryTags);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Request_CarriesUserAgentWithContact_AndOnlyTheBarcode()
    {
        var (api, handler) = Create(_ => Json(HttpStatusCode.OK, FoundJson));

        await api.FetchAsync(Barcode, Ct);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"/api/v2/product/{Barcode}", request.RequestUri!.AbsolutePath);
        Assert.StartsWith("fields=code,product_name", request.RequestUri.Query.TrimStart('?'), StringComparison.Ordinal);
        Assert.DoesNotContain("image", request.RequestUri.Query, StringComparison.Ordinal);
        var userAgent = request.Headers.UserAgent.ToString();
        Assert.StartsWith("FoodAssistant/", userAgent, StringComparison.Ordinal);
        Assert.EndsWith("(https://github.com/gartenkoch7-ctrl/Food-Assistant)", userAgent, StringComparison.Ordinal);
        Assert.Null(request.Headers.Authorization);
        Assert.DoesNotContain(request.Headers, h => h.Key is "Cookie" or "Authorization");
    }

    [Fact]
    public async Task PackageQuantity_AsNumber_IsRead()
    {
        var (api, _) = Create(_ => Json(HttpStatusCode.OK, """{"status":1,"product":{"product_quantity":330.5,"product_quantity_unit":"g"}}"""));

        var lookup = await api.FetchAsync(Barcode, Ct);

        Assert.Equal(330.5m, lookup.Product!.PackageQuantity);
    }

    [Fact]
    public async Task Status0_IsNotFound()
    {
        var (api, _) = Create(_ => Json(HttpStatusCode.OK, """{"code":"4000000000017","status":0,"status_verbose":"product not found"}"""));

        Assert.Equal(FoodFactsStatus.NotFound, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task Http404_IsNotFound()
    {
        var (api, _) = Create(_ => Json(HttpStatusCode.NotFound, """{"status":0}"""));

        Assert.Equal(FoodFactsStatus.NotFound, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task Http503_IsUnavailable()
    {
        var (api, _) = Create(_ => Json(HttpStatusCode.ServiceUnavailable, "down"));

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task MalformedBody_IsUnavailable()
    {
        var (api, _) = Create(_ => Json(HttpStatusCode.OK, "<html>not json</html>"));

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task NetworkError_IsUnavailable()
    {
        var (api, _) = Create(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task ConnectionResetWhileReadingBody_IsUnavailable()
    {
        var (api, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ThrowingStream()) });

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task Timeout_IsUnavailable()
    {
        var handler = new FakeHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, FoundJson);
        });
        var api = Create(handler, new OpenFoodFactsOptions { TimeoutSeconds = 1 });

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        var handler = new FakeHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, FoundJson);
        });
        var api = Create(handler, new OpenFoodFactsOptions());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.FetchAsync(Barcode, cts.Token));
    }

    [Fact]
    public async Task Throttle_RejectsBeyondLimit_WithoutCall()
    {
        var (api, handler) = Create(_ => Json(HttpStatusCode.OK, FoundJson), new OpenFoodFactsOptions { RequestsPerMinute = 2 });

        var statuses = new List<FoodFactsStatus>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await api.FetchAsync(Barcode, Ct)).Status);
        }

        Assert.Equal([FoodFactsStatus.Found, FoodFactsStatus.Found, FoodFactsStatus.Unavailable], statuses);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Disabled_NeverCallsOut()
    {
        var (api, handler) = Create(_ => Json(HttpStatusCode.OK, FoundJson), new OpenFoodFactsOptions { Enabled = false });

        Assert.Equal(FoodFactsStatus.Unavailable, (await api.FetchAsync(Barcode, Ct)).Status);
        Assert.Equal(0, handler.Calls);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (OpenFoodFactsApi Api, FakeHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        OpenFoodFactsOptions? options = null)
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(respond(request)));
        return (Create(handler, options ?? new OpenFoodFactsOptions()), handler);
    }

    private static OpenFoodFactsApi Create(FakeHandler handler, OpenFoodFactsOptions options)
    {
        var wrapped = Options.Create(options);
        var http = new HttpClient(handler);
        return new OpenFoodFactsApi(http, wrapped, new OpenFoodFactsThrottle(wrapped), NullLogger<OpenFoodFactsApi>.Instance);
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("connection reset");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw new IOException("connection reset");
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public int Calls => Requests.Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return respond(request, cancellationToken);
        }
    }
}

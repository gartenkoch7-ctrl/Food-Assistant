using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;

namespace FoodAssistant.Client.Tests.Http;

public sealed class ApiHttpClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Item(string Name);

    private static ApiHttpClient Create(FakeHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });

    [Fact]
    public async Task Get_ReturnsTheValueAndTheETag()
    {
        var handler = new FakeHttpHandler().Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"name":"Milk"}""", System.Text.Encoding.UTF8, "application/json"),
            };
            response.Headers.TryAddWithoutValidation("ETag", "\"7\"");
            return response;
        });

        var result = await Create(handler).GetAsync<Item>("api/v1/things/1", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Milk", result.Value!.Name);
        Assert.Equal("\"7\"", result.ETag);
    }

    [Fact]
    public async Task ProblemWithFieldErrors_BecomesAnApiError()
    {
        var handler = new FakeHttpHandler().EnqueueJson(
            HttpStatusCode.BadRequest,
            """{"status":400,"errorCode":"common.validation_failed","detail":"bad","errors":{"name":["required"]}}""",
            "application/problem+json");

        var result = await Create(handler).GetAsync<Item>("x", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error!.Status);
        Assert.Equal("common.validation_failed", result.Error.ErrorCode);
        Assert.Equal("bad", result.Error.Detail);
        Assert.Equal(["required"], result.Error.FieldErrors["name"]);
    }

    [Fact]
    public async Task PreconditionFailed_CarriesTheCurrentETag()
    {
        var handler = new FakeHttpHandler().EnqueueJson(
            HttpStatusCode.PreconditionFailed,
            """{"status":412,"errorCode":"common.precondition_failed","currentETag":"\"9\""}""",
            "application/problem+json");

        var result = await Create(handler).SendAsync(HttpMethod.Put, "x", new Item("a"), "\"8\"", null, Ct);

        Assert.Equal(412, result.Error!.Status);
        Assert.Equal("\"9\"", result.Error.CurrentETag);
        Assert.Equal("\"8\"", handler.Requests[0].Headers["If-Match"]);
    }

    [Fact]
    public async Task NonProblemError_GetsTheGenericCode()
    {
        var handler = new FakeHttpHandler().EnqueueStatus(HttpStatusCode.BadGateway);

        var result = await Create(handler).GetAsync<Item>("x", Ct);

        Assert.Equal("common.request_failed", result.Error!.ErrorCode);
        Assert.Equal(502, result.Error.Status);
    }

    [Fact]
    public async Task NetworkFailure_IsAnErrorValueNotAnException()
    {
        var handler = new FakeHttpHandler().Enqueue(_ => throw new HttpRequestException("down"));

        var result = await Create(handler).GetAsync<Item>("x", Ct);

        Assert.True(result.Error!.IsNetworkError);
        Assert.Equal(ClientErrorCodes.Network, result.Error.ErrorCode);
    }

    [Fact]
    public async Task Writes_AlwaysCarryAnIdempotencyKey_ThatCallersMayChoose()
    {
        var handler = new FakeHttpHandler()
            .EnqueueJson(HttpStatusCode.OK, """{"name":"a"}""")
            .EnqueueStatus(HttpStatusCode.NoContent);
        var api = Create(handler);
        var key = Guid.CreateVersion7();

        await api.SendAsync<Item, Item>(HttpMethod.Post, "x", new Item("a"), null, null, Ct);
        var second = await api.SendAsync(HttpMethod.Delete, "x", (Item?)null, null, key, Ct);

        Assert.True(Guid.TryParse(handler.Requests[0].Headers["Idempotency-Key"], out var generated));
        Assert.NotEqual(Guid.Empty, generated);
        Assert.Equal(key.ToString("D"), handler.Requests[1].Headers["Idempotency-Key"]);
        Assert.True(second.IsSuccess);
        Assert.Equal("""{"name":"a"}""", handler.Requests[0].Body);
    }

    [Fact]
    public async Task GetStream_ReturnsTheBody()
    {
        var handler = new FakeHttpHandler().EnqueueJson(HttpStatusCode.OK, "hello", "text/plain");

        var result = await Create(handler).GetStreamAsync("x", Ct);

        await using var stream = result.Value!;
        using var reader = new StreamReader(stream);
        Assert.Equal("hello", await reader.ReadToEndAsync(Ct));
    }
}

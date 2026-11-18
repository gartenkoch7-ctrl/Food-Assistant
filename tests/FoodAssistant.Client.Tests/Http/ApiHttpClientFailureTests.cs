using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;

namespace FoodAssistant.Client.Tests.Http;

public sealed class ApiHttpClientFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Item(string Name);

    private readonly SessionExpiry _expiry = new();
    private int _expired;

    public ApiHttpClientFailureTests() => _expiry.Expired += () => _expired++;

    private ApiHttpClient Create(FakeHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") }, _expiry);

    private static FakeHttpHandler Unauthorized() => new FakeHttpHandler().EnqueueJson(
        HttpStatusCode.Unauthorized, """{"status":401,"errorCode":"common.unauthorized"}""", "application/problem+json");

    [Fact]
    public async Task Timeout_IsANetworkError()
    {
        // HttpClient reports its own timeout as a cancellation although the caller did not cancel.
        var handler = new FakeHttpHandler().Enqueue(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        var result = await Create(handler).GetAsync<Item>("x", Ct);

        Assert.True(result.Error!.IsNetworkError);
        Assert.Equal(ClientErrorCodes.Network, result.Error.ErrorCode);
    }

    [Fact]
    public async Task CancellationByTheCaller_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new FakeHttpHandler().Enqueue(_ =>
        {
            cancellation.Cancel();
            throw new TaskCanceledException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).GetAsync<Item>("x", cancellation.Token));
    }

    [Fact]
    public async Task SuccessWithUnreadableBody_IsAFailedRequest()
    {
        var handler = new FakeHttpHandler().EnqueueJson(HttpStatusCode.OK, "<html>proxy</html>", "application/json");

        var result = await Create(handler).GetAsync<Item>("x", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(200, result.Error!.Status);
        Assert.Equal("common.request_failed", result.Error.ErrorCode);
    }

    [Theory]
    [InlineData("api/v1/households/018f0000-0000-7000-8000-000000000001/items")]
    [InlineData("api/v1/foodfacts/4001234567890")]
    public async Task Unauthorized_OnADataCall_ReportsTheExpiredSession(string path)
    {
        var result = await Create(Unauthorized()).GetAsync<Item>(path, Ct);

        Assert.Equal(401, result.Error!.Status);
        Assert.Equal(1, _expired);
    }

    [Fact]
    public async Task Unauthorized_OnADataWrite_ReportsTheExpiredSession()
    {
        var result = await Create(Unauthorized()).SendAsync(HttpMethod.Delete, "api/v1/households/x/items/1", (Item?)null, null, null, Ct);

        Assert.Equal(401, result.Error!.Status);
        Assert.Equal(1, _expired);
    }

    [Theory]
    [InlineData("api/v1/me")]
    [InlineData("api/v1/setup")]
    [InlineData("api/v1/auth/login")]
    public async Task Unauthorized_OnASessionCall_IsLeftToTheSession(string path)
    {
        var result = await Create(Unauthorized()).GetAsync<Item>(path, Ct);

        Assert.Equal(401, result.Error!.Status);
        Assert.Equal(0, _expired);
    }

    [Fact]
    public async Task OtherErrors_DoNotReportAnExpiredSession()
    {
        var handler = new FakeHttpHandler().EnqueueStatus(HttpStatusCode.Forbidden);

        await Create(handler).GetAsync<Item>("api/v1/households/x/items", Ct);

        Assert.Equal(0, _expired);
    }
}

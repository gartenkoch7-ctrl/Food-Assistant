using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Api;

public sealed class IdempotencyTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private TestState State => factory.Services.GetRequiredService<TestState>();

    [Fact]
    public async Task Retry_ReplaysStoredResponseWithoutRunningHandlerAgain()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await PostItemAsync(client, key, "Milk");
        var createdAfterFirst = State.Created;
        using var second = await PostItemAsync(client, key, "Milk");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(createdAfterFirst, State.Created);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(await BodyAsync(first), await BodyAsync(second));
        Assert.Equal("application/json", second.Content.Headers.ContentType?.MediaType);
        Assert.False(first.Headers.Contains(IdempotencyDefaults.ReplayedHeaderName));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(IdempotencyDefaults.ReplayedHeaderName)));
    }

    [Fact]
    public async Task SameKeyWithDifferentBody_IsKeyReusedProblem()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await PostItemAsync(client, key, "Milk");
        using var second = await PostItemAsync(client, key, "Bread");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await ProblemAssert.IsProblemAsync(second, HttpStatusCode.UnprocessableEntity, ErrorCodes.IdempotencyKeyReused);
    }

    [Fact]
    public async Task SameKeyWithOtherIfMatch_IsKeyReusedNotStaleReplay()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var stale = await SendAsync(client, HttpMethod.Put, "/versioned", key, ifMatch: "\"2\"");
        using var refreshed = await SendAsync(client, HttpMethod.Put, "/versioned", key, ifMatch: "\"3\"");

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await ProblemAssert.IsProblemAsync(refreshed, HttpStatusCode.UnprocessableEntity, ErrorCodes.IdempotencyKeyReused);
    }

    [Fact]
    public async Task SameKeyOnOtherPath_IsIndependentRequest()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await PostItemAsync(client, key, "Milk");
        using var other = await SendAsync(client, HttpMethod.Post, "/plain", key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.False(other.Headers.Contains(IdempotencyDefaults.ReplayedHeaderName));
    }

    [Fact]
    public async Task ConventionAppliedTwice_StillReplays()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await SendAsync(client, HttpMethod.Post, "/nested/plain", key);
        using var second = await SendAsync(client, HttpMethod.Post, "/nested/plain", key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.Contains(IdempotencyDefaults.ReplayedHeaderName));
        Assert.Equal(await BodyAsync(first), await BodyAsync(second));
    }

    [Fact]
    public async Task SameKeyOnPathInOtherCase_IsKeyReusedNotReplay()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await SendAsync(client, HttpMethod.Post, "/plain", key);
        using var other = await SendAsync(client, HttpMethod.Post, "/PLAIN", key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await ProblemAssert.IsProblemAsync(other, HttpStatusCode.UnprocessableEntity, ErrorCodes.IdempotencyKeyReused);
    }

    [Fact]
    public async Task PolymorphicResult_IsSerializedTheSameWithAndWithoutKey()
    {
        using var client = factory.CreateClient();

        using var withKey = await SendAsync(client, HttpMethod.Post, "/polymorphic", Guid.NewGuid());
        using var withoutKey = await client.PostAsync(new Uri($"{TestEndpoints.Root}/polymorphic", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        var expected = await BodyAsync(withoutKey);
        Assert.Contains("\"$type\":\"derived\"", expected, StringComparison.Ordinal);
        Assert.Equal(expected, await BodyAsync(withKey));
    }

    [Fact]
    public async Task WithoutKey_EveryRequestRuns()
    {
        using var client = factory.CreateClient();
        var before = State.Created;

        using var first = await PostItemAsync(client, key: null, "Milk");
        using var second = await PostItemAsync(client, key: null, "Milk");

        Assert.Equal(before + 2, State.Created);
    }

    [Fact]
    public async Task InvalidKey_IsBadRequestProblem()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{TestEndpoints.Root}/plain", UriKind.Relative));
        request.Headers.Add(IdempotencyDefaults.HeaderName, "not-a-uuid");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.IdempotencyKeyInvalid);
    }

    [Fact]
    public async Task ServerError_IsNotStoredSoRetryRunsAgain()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await SendAsync(client, HttpMethod.Post, "/flaky", key);
        using var retry = await SendAsync(client, HttpMethod.Post, "/flaky", key);

        await ProblemAssert.IsProblemAsync(first, HttpStatusCode.ServiceUnavailable, ErrorCodes.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task ReplayedEmptyErrorResponse_StillHasProblemBody()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        using var first = await SendAsync(client, HttpMethod.Delete, "/missing", key);
        using var retry = await SendAsync(client, HttpMethod.Delete, "/missing", key);

        await ProblemAssert.IsProblemAsync(first, HttpStatusCode.NotFound, ErrorCodes.NotFound);
        await ProblemAssert.IsProblemAsync(retry, HttpStatusCode.NotFound, ErrorCodes.NotFound);
        Assert.True(retry.Headers.Contains(IdempotencyDefaults.ReplayedHeaderName));
    }

    [Fact]
    public async Task ConcurrentRetry_IsInProgressConflict()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();

        var firstTask = SendAsync(client, HttpMethod.Post, "/slow", key);
        await State.SlowEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var concurrent = await SendAsync(client, HttpMethod.Post, "/slow", key);
        State.SlowRelease.TrySetResult();
        using var first = await firstTask;

        await ProblemAssert.IsProblemAsync(concurrent, HttpStatusCode.Conflict, ErrorCodes.IdempotencyRequestInProgress);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    }

    private static Task<HttpResponseMessage> PostItemAsync(HttpClient client, Guid? key, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{TestEndpoints.Root}/items", UriKind.Relative))
        {
            Content = JsonContent.Create(new { name, quantity = 1 }),
        };
        if (key is not null)
        {
            request.Headers.Add(IdempotencyDefaults.HeaderName, key.Value.ToString());
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, Guid key, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, new Uri(TestEndpoints.Root + path, UriKind.Relative));
        request.Headers.Add(IdempotencyDefaults.HeaderName, key.ToString());
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            request.Content = JsonContent.Create(new { name = "Milk", quantity = 1 });
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<string> BodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}

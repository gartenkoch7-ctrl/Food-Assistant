using System.Diagnostics;
using System.Net;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Client.Http;

namespace FoodAssistant.Client.Tests.Http;

public sealed class TraceContextHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<RecordedRequest> SendAsync(Action<HttpRequestMessage>? configure = null)
    {
        var fake = new FakeHttpHandler().EnqueueStatus(HttpStatusCode.OK);
        using var http = new HttpClient(new TraceContextHandler { InnerHandler = fake }) { BaseAddress = new Uri("https://food.test/") };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("x", UriKind.Relative));
        configure?.Invoke(request);
        using var response = await http.SendAsync(request, Ct);
        return fake.Requests[0];
    }

    [Fact]
    public async Task WithoutAnActivity_ANewValidTraceparentIsCreated()
    {
        var sent = await SendAsync();

        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-01$", sent.Headers["traceparent"]);
    }

    [Fact]
    public async Task WithAnActivity_ItsIdIsUsed()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();

        var sent = await SendAsync();

        Assert.Equal(activity.Id, sent.Headers["traceparent"]);
    }

    [Fact]
    public async Task AnExistingTraceparent_IsKept()
    {
        var sent = await SendAsync(r => r.Headers.TryAddWithoutValidation("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01"));

        Assert.Equal("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01", sent.Headers["traceparent"]);
    }
}

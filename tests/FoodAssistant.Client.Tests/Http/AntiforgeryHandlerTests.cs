using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;

namespace FoodAssistant.Client.Tests.Http;

public sealed class AntiforgeryHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Token1 = """{"requestToken":"t1","headerName":"X-XSRF-TOKEN"}""";
    private const string Token2 = """{"requestToken":"t2","headerName":"X-XSRF-TOKEN"}""";
    private const string Stale = """{"status":400,"errorCode":"accounts.antiforgery_invalid"}""";

    private static HttpClient Create(FakeHttpHandler fake) =>
        new(new AntiforgeryHandler { InnerHandler = fake }) { BaseAddress = new Uri("https://food.test/") };

    private static Task<HttpResponseMessage> PostAsync(HttpClient http) =>
        http.PostAsync(new Uri("api/v1/x", UriKind.Relative), new StringContent("""{"a":1}""", System.Text.Encoding.UTF8, "application/json"), Ct);

    [Fact]
    public async Task Reads_NeedNoToken()
    {
        var fake = new FakeHttpHandler().EnqueueStatus(HttpStatusCode.OK);
        using var http = Create(fake);

        await http.GetAsync(new Uri("api/v1/x", UriKind.Relative), Ct);

        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task Writes_FetchTheTokenOnce_AndSendItAsHeader()
    {
        var fake = new FakeHttpHandler()
            .EnqueueJson(HttpStatusCode.OK, Token1)
            .EnqueueStatus(HttpStatusCode.NoContent)
            .EnqueueStatus(HttpStatusCode.NoContent);
        using var http = Create(fake);

        await PostAsync(http);
        await PostAsync(http);

        Assert.Equal(3, fake.Requests.Count);
        Assert.EndsWith("antiforgery", fake.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal("t1", fake.Requests[1].Headers["X-XSRF-TOKEN"]);
        Assert.Equal("t1", fake.Requests[2].Headers["X-XSRF-TOKEN"]);
    }

    [Fact]
    public async Task StaleToken_IsRefreshedAndTheRequestRepeatedWithItsBody()
    {
        var fake = new FakeHttpHandler()
            .EnqueueJson(HttpStatusCode.OK, Token1)
            .EnqueueJson(HttpStatusCode.BadRequest, Stale, "application/problem+json")
            .EnqueueJson(HttpStatusCode.OK, Token2)
            .EnqueueStatus(HttpStatusCode.NoContent);
        using var http = Create(fake);

        using var response = await PostAsync(http);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(4, fake.Requests.Count);
        Assert.Equal("t2", fake.Requests[3].Headers["X-XSRF-TOKEN"]);
        Assert.Equal("""{"a":1}""", fake.Requests[3].Body);
    }

    [Fact]
    public async Task FailedTokenRequest_IsPassedOnAsTheAnswer()
    {
        var fake = new FakeHttpHandler()
            .EnqueueJson(HttpStatusCode.BadRequest, """{"status":400,"errorCode":"accounts.https_required"}""", "application/problem+json");
        using var http = Create(fake);

        using var response = await PostAsync(http);

        // The write itself is never sent without a token; the caller reads the server's reason instead.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(fake.Requests);
        Assert.Contains("https_required", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OtherBadRequests_AreNotRetried()
    {
        var fake = new FakeHttpHandler()
            .EnqueueJson(HttpStatusCode.OK, Token1)
            .EnqueueJson(HttpStatusCode.BadRequest, """{"errorCode":"common.validation_failed"}""", "application/problem+json");
        using var http = Create(fake);

        using var response = await PostAsync(http);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Contains("validation_failed", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}

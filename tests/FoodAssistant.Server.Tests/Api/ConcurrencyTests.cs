using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Concurrency;

namespace FoodAssistant.Server.Tests.Api;

public sealed class ConcurrencyTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public void FromVersion_IsQuotedStrongETag()
    {
        Assert.Equal("\"42\"", ETags.FromVersion(42));
    }

    [Theory]
    [InlineData("\"3\"")]
    [InlineData("\"1\", \"3\"")]
    [InlineData("W/\"3\"")]
    [InlineData("*")]
    public async Task MatchingIfMatch_UpdatesAndReturnsNewETag(string ifMatch)
    {
        using var response = await PutAsync(ifMatch);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"4\"", response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task MissingIfMatch_IsPreconditionRequiredProblem()
    {
        using var response = await PutAsync(ifMatch: null);

        await ProblemAssert.IsProblemAsync(response, (HttpStatusCode)428, ErrorCodes.PreconditionRequired);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("not-an-etag")]
    [InlineData("42, \"3\"")]
    public async Task MalformedIfMatch_IsBadRequestProblem(string ifMatch)
    {
        using var response = await PutAsync(ifMatch);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.BadRequest);
    }

    [Fact]
    public async Task MissingIfMatch_IsAllowedWhenNotRequired()
    {
        using var response = await PutAsync(ifMatch: null, path: "/versioned-optional");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("\"2\"")]
    [InlineData("W/\"2\"")]
    public async Task StaleIfMatch_IsPreconditionFailedWithCurrentState(string ifMatch)
    {
        using var response = await PutAsync(ifMatch);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        Assert.Equal("\"3\"", response.Headers.ETag?.ToString());
        Assert.Equal("\"3\"", problem.GetProperty(ETags.CurrentETagField).GetString());
        Assert.Equal("stored", problem.GetProperty(ETags.CurrentField).GetProperty("name").GetString());
    }

    private async Task<HttpResponseMessage> PutAsync(string? ifMatch, string path = "/versioned")
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TestEndpoints.Root + path, UriKind.Relative))
        {
            Content = JsonContent.Create(new { name = "Milk", quantity = 1 }),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

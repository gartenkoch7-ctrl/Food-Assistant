using System.Net;
using System.Net.Http.Headers;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Server.Tests.Api;

public sealed class ProblemDetailsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task FeatureProblem_KeepsItsErrorCodeAndDetail()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{TestEndpoints.Root}/feature-problem", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "test.item_not_found");
        Assert.Equal("No such item.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BareStatusResult_GetsProblemBodyFromStatusCodePages()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{TestEndpoints.Root}/bare-not-found", UriKind.Relative), TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task UnknownApiRoute_IsNotFoundProblem()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task WrongMethod_IsMethodNotAllowedProblem()
    {
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.MethodNotAllowed, ErrorCodes.MethodNotAllowed);
    }

    [Fact]
    public async Task HtmlAcceptHeader_StillGetsProblemJsonOnApiRoutes()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{TestEndpoints.Root}/bare-not-found", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task TraceId_IsW3CTraceParent()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{TestEndpoints.Root}/bare-not-found", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", problem.GetProperty("traceId").GetString());
    }
}

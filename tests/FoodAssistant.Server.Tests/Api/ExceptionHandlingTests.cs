using System.Net;
using System.Text;
using FoodAssistant.Contracts.Common;
using Microsoft.AspNetCore.Hosting;

namespace FoodAssistant.Server.Tests.Api;

public sealed class ExceptionHandlingTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task UnhandledException_Is500WithoutInternalDetails(string environment)
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = configured.CreateClient();

        using var response = await client.GetAsync(new Uri($"{TestEndpoints.Root}/throw", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.InternalServerError, ErrorCodes.InternalError);
        var body = problem.GetRawText();
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task MalformedJsonBody_IsBadRequestProblem()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri($"{TestEndpoints.Root}/validated", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.BadRequest);
    }

    [Fact]
    public async Task WrongContentType_IsUnsupportedMediaTypeProblem()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("name=x", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(new Uri($"{TestEndpoints.Root}/validated", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.UnsupportedMediaType, ErrorCodes.UnsupportedMediaType);
    }
}

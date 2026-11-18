using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Server.Tests.Api;

public sealed class ValidationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task InvalidBody_IsValidationProblemWithFieldErrors()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"{TestEndpoints.Root}/validated", UriKind.Relative),
            new { name = new string('x', 21), quantity = 0 },
            TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        var errors = problem.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Array, errors.GetProperty("quantity").ValueKind);
    }

    [Fact]
    public async Task ValidBody_PassesValidation()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"{TestEndpoints.Root}/validated", UriKind.Relative),
            new { name = "Milk", quantity = 2 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InvalidQueryParameter_IsValidationProblem()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{TestEndpoints.Root}/range?count=11", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.True(problem.GetProperty("errors").TryGetProperty("count", out _));
    }

    [Fact]
    public async Task ManualValidationProblem_HasErrorCodeAndErrors()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(new Uri($"{TestEndpoints.Root}/manual-validation", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        Assert.Equal("Name is taken.", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }
}

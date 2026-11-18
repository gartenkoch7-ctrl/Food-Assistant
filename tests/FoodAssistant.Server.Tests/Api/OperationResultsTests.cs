using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Concurrency;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>The one mapping of use-case results to HTTP (ADR-0004).</summary>
public sealed class OperationResultsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Ok_Is200WithETag()
    {
        using var response = await GetAsync(OperationStatus.Ok);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ETags.FromVersion(OperationResultTestEndpoints.Version), response.Headers.ETag?.ToString());
        var body = await response.Content.ReadFromJsonAsync<TestVersionedItem>(TestContext.Current.CancellationToken);
        Assert.Equal("milk", body!.Name);
    }

    [Fact]
    public async Task Created_Is201WithETagAndLocation()
    {
        using var response = await GetAsync(OperationStatus.Created);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ETags.FromVersion(OperationResultTestEndpoints.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{TestEndpoints.Root}/items/milk", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Deleted_Is204()
    {
        using var response = await GetAsync(OperationStatus.Deleted);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(OperationStatus.NotFound, HttpStatusCode.NotFound, "test.item_not_found")]
    [InlineData(OperationStatus.Conflict, HttpStatusCode.Conflict, "test.item_exists")]
    [InlineData(OperationStatus.RuleViolated, HttpStatusCode.UnprocessableEntity, "test.item_in_use")]
    [InlineData(OperationStatus.Forbidden, HttpStatusCode.Forbidden, "test.role_insufficient")]
    public async Task Failures_AreProblemsWithTheirCode(OperationStatus status, HttpStatusCode expected, string errorCode)
    {
        using var response = await GetAsync(status);

        await ProblemAssert.IsProblemAsync(response, expected, errorCode);
    }

    [Fact]
    public async Task Invalid_Is400WithFieldErrors()
    {
        using var response = await GetAsync(OperationStatus.Invalid);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "test.validation_failed");
        Assert.Equal("test.name_required", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task VersionMismatch_Is412WithCurrentETagAndState()
    {
        using var response = await GetAsync(OperationStatus.VersionMismatch);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        Assert.Equal(ETags.FromVersion(OperationResultTestEndpoints.Version), response.Headers.ETag?.ToString());
        Assert.Equal("stored", problem.GetProperty(ETags.CurrentField).GetProperty("name").GetString());
    }

    private async Task<HttpResponseMessage> GetAsync(OperationStatus status)
    {
        using var client = factory.CreateClient();
        return await client.GetAsync(
            new Uri($"{TestEndpoints.Root}{OperationResultTestEndpoints.Path}/{status}", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }
}

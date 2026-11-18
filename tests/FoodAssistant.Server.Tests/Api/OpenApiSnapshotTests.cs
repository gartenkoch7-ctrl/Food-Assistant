using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodAssistant.Server.Api.Hosting;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>
/// Compares the generated <c>/openapi/v1.json</c> with the checked-in snapshot
/// <c>tests/FoodAssistant.Server.Tests/Api/openapi-v1.json</c> (ADR-0004), so unintended API
/// changes fail the build.
/// </summary>
/// <remarks>
/// Intended API change? Regenerate the snapshot, review its diff and commit it with the change:
/// <code>
/// $env:UPDATE_OPENAPI_SNAPSHOT = "1"           # bash: export UPDATE_OPENAPI_SNAPSHOT=1
/// dotnet test --solution FoodAssistant.slnx
/// Remove-Item Env:UPDATE_OPENAPI_SNAPSHOT      # bash: unset UPDATE_OPENAPI_SNAPSHOT
/// git diff tests/FoodAssistant.Server.Tests/Api/openapi-v1.json
/// </code>
/// With the variable set the test rewrites the file and passes. Breaking changes inside
/// <c>/api/v1</c> are not allowed (AGENTS.md 2.4); they belong in <c>/api/v2</c>.
/// </remarks>
public sealed class OpenApiSnapshotTests(PostgresContainerFixture postgres)
{
    private const string UpdateVariable = "UPDATE_OPENAPI_SNAPSHOT";
    private const string SnapshotFile = "openapi-v1.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    [Fact]
    public async Task OpenApiDocument_MatchesSnapshot()
    {
        // The plain server without test endpoints: the snapshot shows the product API only.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment(HostingOptions.TestingEnvironment)
                .UseSetting("ConnectionStrings:FoodAssistant", postgres.NewDatabaseConnectionString()));
        using var client = factory.CreateClient();

        var actualJson = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var actual = JsonNode.Parse(actualJson);
        var snapshotPath = Path.Combine(ProjectDirectory(), "Api", SnapshotFile);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(snapshotPath, actual!.ToJsonString(WriteOptions) + Environment.NewLine, TestContext.Current.CancellationToken);
            return;
        }

        Assert.True(File.Exists(snapshotPath), $"Snapshot {snapshotPath} is missing. Create it with {UpdateVariable}=1 (see test remarks).");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath, TestContext.Current.CancellationToken));
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"The OpenAPI document differs from {SnapshotFile}. If the change is intended, regenerate the snapshot with {UpdateVariable}=1 "
            + "and review the diff (see test remarks). Current document:" + Environment.NewLine + actual!.ToJsonString(WriteOptions));
    }

    private static string ProjectDirectory() =>
        typeof(OpenApiSnapshotTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "ProjectDirectory").Value!;
}

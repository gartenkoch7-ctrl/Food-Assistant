using System.Net;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Sync;

/// <summary>Live updates over the household hub (ADR-0005), without a browser: long polling on the test server.</summary>
public sealed class HouseholdHubTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Member_Joins_AndReceivesChangedAfterAProductUpdate()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await using var connection = Connect(app, await AccessTokenAsync(app, admin));
        var received = new TaskCompletionSource<HouseholdChangesMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<HouseholdChangesMessage>(HouseholdHubContract.Changed, message => received.TrySetResult(message));
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(HouseholdHubContract.Join, setup.HouseholdId, Ct);

        await RenameProductAsync(app, setup.HouseholdId, product.Id);

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        Assert.Equal(setup.HouseholdId, message.HouseholdId);
        var change = Assert.Single(message.Changes);
        Assert.Equal((SyncEntityTypes.Product, product.Id, false), (change.EntityType, change.Id, change.Deleted));
        Assert.True(change.Sequence > 0);
    }

    [Fact]
    public async Task Connection_ReceivesOnlyTheHouseholdsItJoined()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var other = await Scenario.CreateHouseholdAsync(admin, "Other");
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await using var connection = Connect(app, await AccessTokenAsync(app, admin));
        var received = new TaskCompletionSource<HouseholdChangesMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<HouseholdChangesMessage>(HouseholdHubContract.Changed, message => received.TrySetResult(message));
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(HouseholdHubContract.Join, other, Ct);

        await RenameProductAsync(app, setup.HouseholdId, product.Id);
        var otherProduct = await HouseholdDataSeed.ProductAsync(app.Services, other);

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        Assert.Equal(other, message.HouseholdId);
        Assert.Equal(otherProduct.Id, Assert.Single(message.Changes).Id);
    }

    [Fact]
    public async Task NonMember_CannotJoin()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Other");
        var (member, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "member");
        using var __ = member;
        await using var connection = Connect(app, await AccessTokenAsync(app, member));
        await connection.StartAsync(Ct);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync(HouseholdHubContract.Join, foreign, Ct));

        Assert.Contains(HouseholdErrorCodes.NotFound, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutSignIn_ConnectionIsRejected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        await using var connection = Connect(app, accessToken: null);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync(Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    private static HubConnection Connect(ServerTestApp app, string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost" + HouseholdHubContract.Path), HttpTransportType.LongPolling, options =>
            {
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                if (accessToken is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .Build();

    // The app's way in (ADR-0003): PKCE via the signed-in browser session, then a bearer token.
    private static async Task<string> AccessTokenAsync(ServerTestApp app, TestClient browser)
    {
        using var phone = app.CreateTestClient();
        var tokens = await OidcFlow.SignInAppAsync(browser, phone);
        return tokens.GetProperty("access_token").GetString()!;
    }

    private static async Task RenameProductAsync(ServerTestApp app, Guid householdId, Guid productId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<Product>().SingleAsync(p => p.Id == productId, Ct);
        product.Name = "Oat milk";
        await db.SaveChangesAsync(Ct);
    }
}

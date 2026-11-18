using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Sync;

/// <summary>The web client of live updates (M1 E1) against the real hub.</summary>
public sealed class LiveClientTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Client_ConnectsJoins_AndReportsChangesOfTheActiveHousehold()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await using var live = await LiveClientSupport.CreateAsync(app, admin);
        var received = LiveClientSupport.Collect(live);

        await live.StartAsync(setup.HouseholdId, Ct);
        await LiveClientSupport.WaitForStateAsync(live, LiveConnectionState.Connected);
        await LiveClientSupport.RenameProductAsync(app, setup.HouseholdId, product.Id, "Oat milk");

        await LiveClientSupport.WaitForAsync(() => received.Count > 0);
        Assert.Equal(setup.HouseholdId, received[0].HouseholdId);
        Assert.Equal(product.Id, Assert.Single(received[0].Changes).Id);
    }

    [Fact]
    public async Task Client_StartsWithoutWaiting_AndIsConnectingMeanwhile()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        await using var live = await LiveClientSupport.CreateAsync(app, admin);

        await live.StartAsync(setup.HouseholdId, Ct);

        Assert.True(live.State is LiveConnectionState.Connecting or LiveConnectionState.Connected);
    }

    [Fact]
    public async Task Client_AfterSwitchingHousehold_HearsOnlyTheNewOne()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var other = await Scenario.CreateHouseholdAsync(admin, "Other");
        var first = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        var second = await HouseholdDataSeed.ProductAsync(app.Services, other);
        await using var live = await LiveClientSupport.CreateAsync(app, admin);
        var received = LiveClientSupport.Collect(live);
        await live.StartAsync(setup.HouseholdId, Ct);
        await LiveClientSupport.WaitForStateAsync(live, LiveConnectionState.Connected);

        await live.StopAsync();
        await live.StartAsync(other, Ct);
        await LiveClientSupport.WaitForStateAsync(live, LiveConnectionState.Connected);
        await LiveClientSupport.RenameProductAsync(app, setup.HouseholdId, first.Id, "Old household");
        await LiveClientSupport.RenameProductAsync(app, other, second.Id, "New household");

        await LiveClientSupport.WaitForAsync(() => received.Count > 0);
        await Task.Delay(300, Ct);
        Assert.All(received, message => Assert.Equal(other, message.HouseholdId));
    }

    [Fact]
    public async Task Client_IgnoresMessagesOfAnotherHousehold()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var other = await Scenario.CreateHouseholdAsync(admin, "Other");
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await using var live = await LiveClientSupport.CreateAsync(app, admin);
        var received = LiveClientSupport.Collect(live);
        await live.StartAsync(setup.HouseholdId, Ct);
        await LiveClientSupport.WaitForStateAsync(live, LiveConnectionState.Connected);

        // A second connection of the same person listens to the other household; the first one must not hear it.
        await using var second = await LiveClientSupport.CreateAsync(app, admin);
        var secondReceived = LiveClientSupport.Collect(second);
        await second.StartAsync(other, Ct);
        await LiveClientSupport.WaitForStateAsync(second, LiveConnectionState.Connected);
        var otherProduct = await HouseholdDataSeed.ProductAsync(app.Services, other);
        await LiveClientSupport.RenameProductAsync(app, other, otherProduct.Id, "Elsewhere");
        await LiveClientSupport.WaitForAsync(() => secondReceived.Count > 0);
        await LiveClientSupport.RenameProductAsync(app, setup.HouseholdId, product.Id, "Here");

        await LiveClientSupport.WaitForAsync(() => received.Count > 0);
        Assert.All(received, message => Assert.Equal(setup.HouseholdId, message.HouseholdId));
    }

    [Fact]
    public async Task Join_WithoutMembership_EndsDisconnected()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Other");
        var (member, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "member");
        using var __ = member;
        await using var live = await LiveClientSupport.CreateAsync(app, member);

        await live.StartAsync(foreign, Ct);

        await LiveClientSupport.WaitForStateAsync(live, LiveConnectionState.Disconnected);
        // Refused for good: no retry loop that would hammer the server.
        await Task.Delay(600, Ct);
        Assert.Equal(LiveConnectionState.Disconnected, live.State);
    }
}

using System.Net;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Sync;

/// <summary>A removed member's open connection stops receiving the household's changes at once (ADR-0005).</summary>
public sealed class RemovedMemberLiveTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemovedMember_NoLongerReceivesChanges_WhileOthersStillDo()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (member, memberId) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "member");
        using var __ = member;
        var product = await HouseholdDataSeed.ProductAsync(app.Services, setup.HouseholdId);
        await using var memberLive = await LiveClientSupport.CreateAsync(app, member);
        await using var adminLive = await LiveClientSupport.CreateAsync(app, admin);
        var memberReceived = LiveClientSupport.Collect(memberLive);
        var adminReceived = LiveClientSupport.Collect(adminLive);
        await memberLive.StartAsync(setup.HouseholdId, Ct);
        await adminLive.StartAsync(setup.HouseholdId, Ct);
        await LiveClientSupport.WaitForStateAsync(memberLive, LiveConnectionState.Connected);
        await LiveClientSupport.WaitForStateAsync(adminLive, LiveConnectionState.Connected);
        await LiveClientSupport.RenameProductAsync(app, setup.HouseholdId, product.Id, "Before removal");
        await LiveClientSupport.WaitForAsync(() => memberReceived.Count > 0 && adminReceived.Count > 0);
        var memberBefore = memberReceived.Count;

        using var removal = await admin.DeleteAsync($"/api/v1/households/{setup.HouseholdId}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        var adminBefore = adminReceived.Count;
        await LiveClientSupport.RenameProductAsync(app, setup.HouseholdId, product.Id, "After removal");

        await LiveClientSupport.WaitForAsync(() => adminReceived.Count > adminBefore);
        await Task.Delay(500, Ct);
        Assert.Equal(memberBefore, memberReceived.Count);
    }
}

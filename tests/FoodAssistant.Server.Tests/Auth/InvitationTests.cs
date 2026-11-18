using System.Net;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

public sealed class InvitationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Invitation_CreateAndAccept_CreatesAccountWithInvitedRole()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;

        var invitation = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Restricted);
        var (guest, guestId) = await Scenario.JoinAsNewUserAsync(app, invitation.Token, "guest");
        using var __ = guest;

        Assert.False(string.IsNullOrWhiteSpace(invitation.Token));
        Assert.InRange(invitation.ExpiresAt, app.Time.GetUtcNow().AddDays(6.9), app.Time.GetUtcNow().AddDays(7.1));
        var me = await guest.GetJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal(guestId, me.UserId);
        Assert.False(me.IsServerAdmin);
        var membership = Assert.Single(me.Households);
        Assert.Equal((setup.HouseholdId, HouseholdRole.Restricted), (membership.Id, membership.Role));
    }

    [Fact]
    public async Task Invitation_List_ShowsStatusButNeverTheToken()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var accepted = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);
        var pending = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);
        (await Scenario.JoinAsNewUserAsync(app, accepted.Token, "guest")).Client.Dispose();

        using var response = await admin.GetAsync($"/api/v1/households/{setup.HouseholdId}/invitations");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = await TestClient.ReadAsync<List<InvitationResponse>>(response);

        Assert.DoesNotContain(accepted.Token, body, StringComparison.Ordinal);
        Assert.DoesNotContain(pending.Token, body, StringComparison.Ordinal);
        Assert.Equal(InvitationStatus.Accepted, list.Single(i => i.Id == accepted.Id).Status);
        Assert.Equal(InvitationStatus.Pending, list.Single(i => i.Id == pending.Id).Status);
    }

    [Fact]
    public async Task Invitation_UsedTwice_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var invitation = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);
        (await Scenario.JoinAsNewUserAsync(app, invitation.Token, "first")).Client.Dispose();

        using var second = await AcceptAnonymouslyAsync(app, invitation.Token, "second");

        await TestClient.AssertProblemAsync(second, HttpStatusCode.NotFound, InvitationErrorCodes.InvalidOrExpired);
    }

    [Fact]
    public async Task Invitation_Expired_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var invitation = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);

        app.Time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
        using var response = await AcceptAnonymouslyAsync(app, invitation.Token, "late");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, InvitationErrorCodes.InvalidOrExpired);
    }

    [Fact]
    public async Task Invitation_Revoked_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var invitation = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);

        using (var revoke = await admin.DeleteAsync($"/api/v1/households/{setup.HouseholdId}/invitations/{invitation.Id}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        }

        using var response = await AcceptAnonymouslyAsync(app, invitation.Token, "revoked");
        await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, InvitationErrorCodes.InvalidOrExpired);
    }

    [Fact]
    public async Task Invitation_UnknownToken_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());

        using var response = await AcceptAnonymouslyAsync(app, "not-a-real-token", "someone");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, InvitationErrorCodes.InvalidOrExpired);
    }

    [Theory]
    [InlineData(HouseholdRole.Member)]
    [InlineData(HouseholdRole.Restricted)]
    public async Task Invitation_NonOwnerCannotInvite_Returns403(HouseholdRole role)
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (member, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, role, "notowner");
        using var __ = member;

        using var response = await member.PostAsync(
            $"/api/v1/households/{setup.HouseholdId}/invitations",
            new CreateInvitationRequest(HouseholdRole.Owner));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Invitation_SignedInPerson_JoinsFurtherHousehold_OnlyOnce()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (guest, guestId) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "guest");
        using var __ = guest;
        var second = await Scenario.CreateHouseholdAsync(admin, "Cabin");
        var invitation = await Scenario.InviteAsync(admin, second, HouseholdRole.Member);
        var again = await Scenario.InviteAsync(admin, second, HouseholdRole.Member);

        using (var join = await guest.PostAsync("/api/v1/invitations/accept", new AcceptInvitationRequest(invitation.Token)))
        {
            var accepted = await TestClient.ReadAsync<AcceptInvitationResponse>(join, HttpStatusCode.Created);
            Assert.Equal((guestId, second), (accepted.UserId, accepted.HouseholdId));
        }

        using var twice = await guest.PostAsync("/api/v1/invitations/accept", new AcceptInvitationRequest(again.Token));
        await TestClient.AssertProblemAsync(twice, HttpStatusCode.Conflict, InvitationErrorCodes.AlreadyMember);
        Assert.Equal(2, (await guest.GetJsonAsync<MeResponse>("/api/v1/me")).Households.Count);
    }

    private static async Task<HttpResponseMessage> AcceptAnonymouslyAsync(ServerTestApp app, string token, string userName)
    {
        using var client = app.CreateTestClient();
        return await client.PostAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(token, userName, Scenario.Password, "Guest"));
    }
}

using System.Net;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

public sealed class MemberTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task LastOwner_CannotBeDemotedOrRemoved()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var members = $"/api/v1/households/{setup.HouseholdId}/members";

        using var demote = await admin.PutAsync($"{members}/{setup.UserId}/role", new ChangeMemberRoleRequest(HouseholdRole.Member));
        using var remove = await admin.DeleteAsync($"{members}/{setup.UserId}");

        await TestClient.AssertProblemAsync(demote, HttpStatusCode.Conflict, HouseholdErrorCodes.LastOwner);
        await TestClient.AssertProblemAsync(remove, HttpStatusCode.Conflict, HouseholdErrorCodes.LastOwner);
    }

    [Fact]
    public async Task Owner_ChangesRoleAndRemovesMember()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (guest, guestId) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "guest");
        using var __ = guest;
        var members = $"/api/v1/households/{setup.HouseholdId}/members";

        using (var promote = await admin.PutAsync($"{members}/{guestId}/role", new ChangeMemberRoleRequest(HouseholdRole.Owner)))
        {
            Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);
        }

        // Two owners now: the admin may step down.
        using (var demoteSelf = await admin.PutAsync($"{members}/{setup.UserId}/role", new ChangeMemberRoleRequest(HouseholdRole.Member)))
        {
            Assert.Equal(HttpStatusCode.NoContent, demoteSelf.StatusCode);
        }

        using (var remove = await guest.DeleteAsync($"{members}/{setup.UserId}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        }

        var list = await guest.GetJsonAsync<List<HouseholdMemberResponse>>(members);
        var only = Assert.Single(list);
        Assert.Equal((guestId, HouseholdRole.Owner), (only.UserId, only.Role));
    }

    [Fact]
    public async Task Member_CannotChangeRoles_Returns403()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (member, memberId) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Member, "member");
        using var __ = member;

        using var response = await member.PutAsync(
            $"/api/v1/households/{setup.HouseholdId}/members/{memberId}/role",
            new ChangeMemberRoleRequest(HouseholdRole.Owner));

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Restricted_CannotListMembers_Returns403()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var (restricted, _) = await Scenario.AddMemberAsync(app, admin, setup.HouseholdId, HouseholdRole.Restricted, "kid");
        using var __ = restricted;

        using var response = await restricted.GetAsync($"/api/v1/households/{setup.HouseholdId}/members");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignOrUnknownHousehold_Returns404()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, outsiderId) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;

        using var members = await outsider.GetAsync($"/api/v1/households/{setup.HouseholdId}/members");
        using var invite = await outsider.PostAsync(
            $"/api/v1/households/{setup.HouseholdId}/invitations",
            new CreateInvitationRequest(HouseholdRole.Member));
        using var remove = await outsider.DeleteAsync($"/api/v1/households/{setup.HouseholdId}/members/{setup.UserId}");
        using var unknown = await outsider.GetAsync($"/api/v1/households/{Guid.CreateVersion7()}/members");

        await TestClient.AssertProblemAsync(members, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(invite, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(remove, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(unknown, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);

        // The outsider's own household still works.
        var ownMembers = await outsider.GetJsonAsync<List<HouseholdMemberResponse>>($"/api/v1/households/{foreign}/members");
        Assert.Contains(ownMembers, m => m.UserId == outsiderId);
    }

    [Fact]
    public async Task ForeignHouseholdInvitation_CannotBeRevokedThroughOwnHousehold()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var foreign = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(app, admin, foreign, HouseholdRole.Owner, "outsider");
        using var __ = outsider;
        var invitation = await Scenario.InviteAsync(admin, setup.HouseholdId, HouseholdRole.Member);

        // Owner of household B addresses an invitation of household A via B's route.
        using var response = await outsider.DeleteAsync($"/api/v1/households/{foreign}/invitations/{invitation.Id}");

        await TestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, InvitationErrorCodes.NotFound);
    }
}

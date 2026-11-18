using System.Net;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>Common steps: setup, invitations, signed-in clients. Test data only, no real names.</summary>
internal static class Scenario
{
    public const string Password = "correct horse battery";
    public const string AdminUserName = "admin";

    public static SetupRequest SetupRequest() =>
        new(AdminUserName, Password, "Admin", "Home", "Europe/Berlin");

    /// <summary>Runs the setup and returns the admin, signed in via cookie.</summary>
    public static async Task<(TestClient Admin, SetupResponse Setup)> SetupAsync(ServerTestApp app)
    {
        var admin = app.CreateTestClient();
        using var response = await admin.PostAsync("/api/v1/setup", SetupRequest());
        var setup = await TestClient.ReadAsync<SetupResponse>(response, HttpStatusCode.Created);
        using var login = await admin.LoginAsync(AdminUserName, Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return (admin, setup);
    }

    public static async Task<CreatedInvitationResponse> InviteAsync(TestClient owner, Guid householdId, HouseholdRole role)
    {
        using var response = await owner.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new CreateInvitationRequest(role));
        return await TestClient.ReadAsync<CreatedInvitationResponse>(response, HttpStatusCode.Created);
    }

    /// <summary>Accepts an invitation anonymously with a new account and signs that account in.</summary>
    public static async Task<(TestClient Client, Guid UserId)> JoinAsNewUserAsync(
        ServerTestApp app,
        string token,
        string userName)
    {
        var client = app.CreateTestClient();
        using var response = await client.PostAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(token, userName, Password, "Guest"));
        var accepted = await TestClient.ReadAsync<AcceptInvitationResponse>(response, HttpStatusCode.Created);
        using var login = await client.LoginAsync(userName, Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return (client, accepted.UserId);
    }

    /// <summary>Owner invites a new person with <paramref name="role"/>; returns that person signed in.</summary>
    public static async Task<(TestClient Client, Guid UserId)> AddMemberAsync(
        ServerTestApp app,
        TestClient owner,
        Guid householdId,
        HouseholdRole role,
        string userName)
    {
        var invitation = await InviteAsync(owner, householdId, role);
        return await JoinAsNewUserAsync(app, invitation.Token, userName);
    }

    /// <summary>A second household created by the admin (who becomes its first owner).</summary>
    public static async Task<Guid> CreateHouseholdAsync(TestClient admin, string name)
    {
        using var response = await admin.PostAsync("/api/v1/households", new CreateHouseholdRequest(name, "Europe/Vienna"));
        return (await TestClient.ReadAsync<HouseholdResponse>(response, HttpStatusCode.Created)).Id;
    }
}

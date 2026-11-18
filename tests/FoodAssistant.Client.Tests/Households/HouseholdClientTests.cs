using System.Net;
using FoodAssistant.Client.Households;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Client.Tests.Households;

public sealed class HouseholdClientTests
{
    private static readonly Guid HouseholdId = Guid.Parse("018f0000-0000-7000-8000-000000000001");
    private static readonly Guid UserId = Guid.Parse("018f0000-0000-7000-8000-0000000000aa");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (HouseholdClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new HouseholdClient(api), handler);
    }

    [Fact]
    public async Task GetSettings_ReadsTheSettings_AndTheETag()
    {
        var (client, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{HouseholdId}}","name":"Home","timeZone":"Europe/Berlin","expiryLeadDays":2,"version":7}""",
                System.Text.Encoding.UTF8,
                "application/json"),
            Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"7\"") },
        });

        var result = await client.GetSettingsAsync(HouseholdId, Ct);

        Assert.EndsWith($"api/v1/households/{HouseholdId}", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, result.Value!.ExpiryLeadDays);
        Assert.Equal("\"7\"", result.ETag);
    }

    [Fact]
    public async Task UpdateSettings_PutsWithIfMatch_AndReportsAStaleVersion()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.PreconditionFailed, """{"errorCode":"common.precondition_failed","currentETag":"\"9\""}""", "application/problem+json");

        var result = await client.UpdateSettingsAsync(HouseholdId, new UpdateHouseholdRequest("Flat", "Europe/Vienna", 4), "\"7\"", Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal("\"7\"", handler.Requests[0].Headers["If-Match"]);
        Assert.Contains("\"expiryLeadDays\":4", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal(412, result.Error!.Status);
        Assert.Equal("\"9\"", result.Error.CurrentETag);
    }

    [Fact]
    public async Task ListMembers_ReadsNamesAndRoles()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""[{"userId":"{{UserId}}","displayName":"Anna","role":"owner","joinedAt":"2026-11-01T10:00:00+00:00"}]""");

        var result = await client.ListMembersAsync(HouseholdId, Ct);

        Assert.EndsWith($"households/{HouseholdId}/members", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        var member = Assert.Single(result.Value!);
        Assert.Equal((UserId, HouseholdRole.Owner), (member.UserId, member.Role));
    }

    [Fact]
    public async Task ChangeMemberRole_PutsTheRole()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await client.ChangeMemberRoleAsync(HouseholdId, UserId, HouseholdRole.Restricted, Ct);

        Assert.True(result.IsSuccess);
        Assert.EndsWith($"members/{UserId}/role", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"role\":\"restricted\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveMember_Deletes_AndReportsTheLastOwner()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Conflict, """{"errorCode":"households.last_owner"}""", "application/problem+json");

        var result = await client.RemoveMemberAsync(HouseholdId, UserId, Ct);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(HouseholdErrorCodes.LastOwner, result.Error!.ErrorCode);
    }

    [Fact]
    public async Task CreateInvitation_PostsTheRole_AndReadsTheToken()
    {
        var (client, handler) = Create();
        var id = Guid.CreateVersion7();
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{"id":"{{id}}","token":"abc","role":"member","expiresAt":"2026-11-08T10:00:00+00:00"}""");

        var result = await client.CreateInvitationAsync(HouseholdId, HouseholdRole.Member, Ct);

        Assert.EndsWith($"households/{HouseholdId}/invitations", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"role\":\"member\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal("abc", result.Value!.Token);
    }

    [Fact]
    public async Task ListAndRevokeInvitations_UseTheInvitationRoutes()
    {
        var (client, handler) = Create();
        var id = Guid.CreateVersion7();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""[{"id":"{{id}}","role":"member","status":"pending","createdAt":"2026-11-01T10:00:00+00:00","expiresAt":"2026-11-08T10:00:00+00:00"}]""");
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var list = await client.ListInvitationsAsync(HouseholdId, Ct);
        var revoke = await client.RevokeInvitationAsync(HouseholdId, id, Ct);

        Assert.Equal(InvitationStatus.Pending, Assert.Single(list.Value!).Status);
        Assert.True(revoke.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith($"invitations/{id}", handler.Requests[1].Uri.ToString(), StringComparison.Ordinal);
    }
}

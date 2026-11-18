using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FoodAssistant.Server.Sync;

/// <summary>
/// Live updates per household (ADR-0005): a client joins the group of each of its households and
/// receives <c>Changed</c> messages (ids and sequences only) after every commit.
/// </summary>
/// <remarks>
/// Membership is checked when joining. Removing a member takes their connections out of the group
/// at once (<see cref="SignalRMembershipRevocationNotifier"/>), so a removed member hears nothing more.
/// </remarks>
[Authorize]
internal sealed class HouseholdHub(
    IHouseholdStore households,
    HouseholdConnectionRegistry registry,
    ILogger<HouseholdHub> logger) : Hub
{
    public async Task Join(Guid householdId)
    {
        var userId = Context.User!.GetRequiredUserId();

        // Registered before the check, so a removal that happens meanwhile finds the connection.
        registry.Add(Context.ConnectionId, userId, householdId);
        var group = GroupName(householdId);
        try
        {
            await EnsureMemberAsync(householdId, userId).ConfigureAwait(false);
            await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted).ConfigureAwait(false);

            // A removal between the check and the group call missed this connection: look again.
            if (await households.FindMembershipAsync(householdId, userId, Context.ConnectionAborted).ConfigureAwait(false) is null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted).ConfigureAwait(false);
                throw NotFound();
            }
        }
        catch
        {
            registry.Remove(Context.ConnectionId, householdId);
            throw;
        }

        SyncLog.HubJoined(logger, householdId);
    }

    public Task Leave(Guid householdId)
    {
        registry.Remove(Context.ConnectionId, householdId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(householdId), Context.ConnectionAborted);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    internal static string GroupName(Guid householdId) => $"household:{householdId:N}";

    private async Task EnsureMemberAsync(Guid householdId, Guid userId)
    {
        var membership = await households.FindMembershipAsync(householdId, userId, Context.ConnectionAborted)
            .ConfigureAwait(false);
        if (membership is null)
        {
            throw NotFound();
        }
    }

    // Same answer for unknown and foreign households (ADR-0002: no hint that it exists).
    private HubException NotFound()
    {
        SyncLog.HubJoinDenied(logger);
        return new HubException(HouseholdErrorCodes.NotFound);
    }
}

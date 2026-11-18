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
/// Membership is checked when joining. A member removed later keeps receiving ids until the
/// connection ends; the data itself stays protected by the API.
/// </remarks>
[Authorize]
internal sealed class HouseholdHub(IHouseholdStore households, ILogger<HouseholdHub> logger) : Hub
{
    public async Task Join(Guid householdId)
    {
        var userId = Context.User!.GetRequiredUserId();
        var membership = await households.FindMembershipAsync(householdId, userId, Context.ConnectionAborted)
            .ConfigureAwait(false);
        if (membership is null)
        {
            // Same answer for unknown and foreign households (ADR-0002: no hint that it exists).
            SyncLog.HubJoinDenied(logger);
            throw new HubException(HouseholdErrorCodes.NotFound);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(householdId), Context.ConnectionAborted)
            .ConfigureAwait(false);
        SyncLog.HubJoined(logger, householdId);
    }

    public Task Leave(Guid householdId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(householdId), Context.ConnectionAborted);

    internal static string GroupName(Guid householdId) => $"household:{householdId:N}";
}

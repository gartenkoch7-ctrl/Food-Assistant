using FoodAssistant.Application.Sync;
using Microsoft.AspNetCore.SignalR;

namespace FoodAssistant.Server.Sync;

/// <summary>Takes the connections of a removed member out of the household's hub group.</summary>
internal sealed class SignalRMembershipRevocationNotifier(
    HouseholdConnectionRegistry registry,
    IHubContext<HouseholdHub> hub,
    ILogger<SignalRMembershipRevocationNotifier> logger) : IMembershipRevocationNotifier
{
    public async Task MemberRemovedAsync(Guid householdId, Guid userId, CancellationToken cancellationToken)
    {
        var group = HouseholdHub.GroupName(householdId);
        foreach (var connectionId in registry.TakeConnections(userId, householdId))
        {
            try
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken).ConfigureAwait(false);
                SyncLog.ConnectionRevoked(logger, householdId);
            }
            catch (Exception exception)
            {
                // The removal is committed and the API refuses the person anyway; a failing group call must not undo the answer.
                SyncLog.RevocationFailed(logger, householdId, exception);
            }
        }
    }
}

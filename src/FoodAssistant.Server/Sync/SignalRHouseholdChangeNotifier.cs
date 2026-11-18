using FoodAssistant.Application.Sync;
using FoodAssistant.Contracts.Sync;
using Microsoft.AspNetCore.SignalR;

namespace FoodAssistant.Server.Sync;

/// <summary>Sends committed changes to the household's hub group (<see cref="HouseholdHubContract.Changed"/>).</summary>
internal sealed class SignalRHouseholdChangeNotifier(IHubContext<HouseholdHub> hub) : IHouseholdChangeNotifier
{
    public Task PublishAsync(Guid householdId, IReadOnlyList<HouseholdChange> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var message = new HouseholdChangesMessage(
            householdId,
            [.. changes.Select(c => new HouseholdChangeDto(c.EntityType, c.EntityId, c.Sequence, c.Deleted))]);
        // The data is committed: the other members must hear about it even if the caller's request
        // was aborted meanwhile (phone lost its connection), so the request token is not passed on.
        // The default lifetime manager only queues into each connection's buffer.
        return hub.Clients
            .Group(HouseholdHub.GroupName(householdId))
            .SendAsync(HouseholdHubContract.Changed, message, CancellationToken.None);
    }
}

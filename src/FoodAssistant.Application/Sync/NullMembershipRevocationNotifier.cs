namespace FoodAssistant.Application.Sync;

/// <summary>Default without live updates (hosts without SignalR); the server replaces it.</summary>
public sealed class NullMembershipRevocationNotifier : IMembershipRevocationNotifier
{
    public Task MemberRemovedAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
}

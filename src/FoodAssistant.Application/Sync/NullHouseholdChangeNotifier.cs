namespace FoodAssistant.Application.Sync;

/// <summary>Default without live updates (hosts without SignalR); the server replaces it.</summary>
public sealed class NullHouseholdChangeNotifier : IHouseholdChangeNotifier
{
    public Task PublishAsync(Guid householdId, IReadOnlyList<HouseholdChange> changes, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

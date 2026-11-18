namespace FoodAssistant.Application.Sync;

/// <summary>
/// Announces committed changes to the clients of a household (SignalR, ADR-0005). Called by the
/// sync interceptor after the commit; features never call it themselves.
/// </summary>
public interface IHouseholdChangeNotifier
{
    /// <summary>Best effort: clients that miss a message catch up through the change log.</summary>
    Task PublishAsync(Guid householdId, IReadOnlyList<HouseholdChange> changes, CancellationToken cancellationToken);
}

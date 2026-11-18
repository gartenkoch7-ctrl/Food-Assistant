namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// Marks a household entity whose changes are synchronised to clients (ADR-0005). The sync
/// interceptor writes a change-log entry for every insert, update and delete of such an entity and
/// announces it via SignalR after the commit; features only implement this interface.
/// </summary>
public interface ISyncTracked : IHouseholdOwned
{
    Guid Id { get; }
}

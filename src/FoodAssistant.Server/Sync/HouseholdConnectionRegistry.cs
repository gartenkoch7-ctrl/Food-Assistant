namespace FoodAssistant.Server.Sync;

/// <summary>
/// Which hub connection of which person joined which household. A SignalR group only knows
/// connection ids, so removing a member from their groups needs this map. One server process,
/// so memory is enough; entries end with the connection.
/// </summary>
internal sealed class HouseholdConnectionRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, (Guid UserId, HashSet<Guid> Households)> _connections = [];

    public void Add(string connectionId, Guid userId, Guid householdId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var entry))
            {
                entry = (userId, []);
                _connections[connectionId] = entry;
            }

            entry.Households.Add(householdId);
        }
    }

    public void Remove(string connectionId, Guid householdId)
    {
        lock (_sync)
        {
            if (_connections.TryGetValue(connectionId, out var entry) && entry.Households.Remove(householdId)
                && entry.Households.Count == 0)
            {
                _connections.Remove(connectionId);
            }
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            _connections.Remove(connectionId);
        }
    }

    /// <summary>Connection ids of the person that joined the household; they are forgotten for it.</summary>
    public IReadOnlyList<string> TakeConnections(Guid userId, Guid householdId)
    {
        lock (_sync)
        {
            var taken = new List<string>();
            foreach (var (connectionId, entry) in _connections)
            {
                if (entry.UserId == userId && entry.Households.Remove(householdId))
                {
                    taken.Add(connectionId);
                }
            }

            foreach (var connectionId in taken.Where(id => _connections[id].Households.Count == 0))
            {
                _connections.Remove(connectionId);
            }

            return taken;
        }
    }
}

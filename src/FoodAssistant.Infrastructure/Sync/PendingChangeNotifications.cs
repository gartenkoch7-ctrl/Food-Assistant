namespace FoodAssistant.Infrastructure.Sync;

/// <summary>
/// Change-log entries written in the current scope that are not yet announced: collected while
/// saving, announced after the commit, dropped on rollback. Scoped, like the database context.
/// </summary>
internal sealed class PendingChangeNotifications
{
    private readonly List<ChangeLogEntry> _entries = [];

    public bool IsEmpty => _entries.Count == 0;

    /// <summary>
    /// A save with change-log entries is running. EF Core commits its own transaction before it
    /// copies generated values (the sequences) into the entities, so that commit must not announce;
    /// the end of the save does.
    /// </summary>
    public bool SaveInProgress { get; set; }

    /// <summary>Households of the pending entries, sorted (fixed lock order prevents deadlocks).</summary>
    public IReadOnlyList<Guid> Households => [.. _entries.Select(e => e.HouseholdId).Distinct().Order()];

    public void Add(ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }

    /// <summary>Forgets the entries of one failed save; entries of earlier saves stay pending.</summary>
    public void Remove(IEnumerable<ChangeLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            _entries.Remove(entry);
        }
    }

    /// <summary>Returns all pending entries and forgets them (each commit is announced once).</summary>
    public IReadOnlyList<ChangeLogEntry> TakeAll()
    {
        var all = _entries.ToList();
        _entries.Clear();
        return all;
    }

    public void Clear() => _entries.Clear();
}

using System.Collections.Concurrent;
using FoodAssistant.Application.Sync;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Fake notifier that records every announcement; can be told to fail.</summary>
public sealed class RecordingChangeNotifier : IHouseholdChangeNotifier
{
    private readonly ConcurrentQueue<(Guid HouseholdId, IReadOnlyList<HouseholdChange> Changes)> _published = new();

    public bool Fail { get; set; }

    public IReadOnlyList<(Guid HouseholdId, IReadOnlyList<HouseholdChange> Changes)> Published => [.. _published];

    public IReadOnlyList<HouseholdChange> AllChanges => [.. _published.SelectMany(p => p.Changes)];

    public Task PublishAsync(Guid householdId, IReadOnlyList<HouseholdChange> changes, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Hub unavailable (test).");
        }

        _published.Enqueue((householdId, changes));
        return Task.CompletedTask;
    }

    public void Reset() => _published.Clear();
}

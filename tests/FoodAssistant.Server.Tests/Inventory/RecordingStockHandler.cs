using System.Collections.Concurrent;
using FoodAssistant.Application.Inventory;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>Records the stock-changed calls; can be told to fail like a broken handler.</summary>
public sealed class RecordingStockHandler : IStockChangedHandler
{
    public ConcurrentQueue<(Guid HouseholdId, Guid[] ProductIds)> Calls { get; } = new();

    public bool Fail { get; set; }

    public Task OnStockChangedAsync(Guid householdId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        Calls.Enqueue((householdId, [.. productIds]));
        return Fail ? Task.FromException(new InvalidOperationException("Handler failure for the test.")) : Task.CompletedTask;
    }
}

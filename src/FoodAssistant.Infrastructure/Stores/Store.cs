using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Stores;

/// <summary>A store the household shops in; it learns the order of its sections from check-offs.</summary>
public sealed class Store : ISyncTracked
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public required string Name { get; set; }

    /// <summary>Section of the last checked-off entry; start of the next learned transition.</summary>
    public string? LastCheckedSectionKey { get; set; }

    /// <summary>When the last entry was checked off; long pauses start a new shopping trip.</summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

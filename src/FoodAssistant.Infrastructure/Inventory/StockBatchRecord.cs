using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Inventory;

/// <summary>
/// Stored state of a batch (<see cref="StockBatch"/>). Read and written only through
/// <see cref="StockBatchMapping"/>, so the domain rules stay the single source of the dates.
/// </summary>
public sealed class StockBatchRecord : ISyncTracked
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public Guid ProductId { get; set; }

    public Guid StorageLocationId { get; set; }

    public decimal QuantityValue { get; set; }

    /// <summary>Unit code (<c>Unit.Code</c>) of <see cref="QuantityValue"/>.</summary>
    public required string QuantityUnitCode { get; set; }

    public StorageType StorageType { get; set; }

    public BatchState State { get; set; }

    public DateOnly? PackageExpiresOn { get; set; }

    public ExpiryKind PackageExpiryKind { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public ExpiryKind ExpiryKind { get; set; }

    public DateOnly? OpenedOn { get; set; }

    public DateOnly? FrozenOn { get; set; }

    public DateOnly? ThawedOn { get; set; }

    public DateOnly? PreparedOn { get; set; }

    public BatchOrigin Origin { get; init; }

    /// <summary>Household date the batch was added (statistics, FEFO tie-break).</summary>
    public DateOnly AddedOn { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token and ETag (<c>xmin</c>).</summary>
    public uint Version { get; set; }
}

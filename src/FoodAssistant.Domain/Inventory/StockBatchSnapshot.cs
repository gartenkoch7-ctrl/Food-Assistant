using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// The persisted state of a <see cref="StockBatch"/>. Persistence reads it with <see cref="From"/>
/// and rebuilds the batch with <see cref="StockBatch.Restore"/>, so the domain stays free of EF Core.
/// </summary>
public sealed record StockBatchSnapshot(
    Guid Id,
    Quantity Quantity,
    StorageType StorageType,
    BatchState State,
    DateOnly? PackageExpiresOn,
    ExpiryKind PackageExpiryKind,
    DateOnly? ExpiresOn,
    ExpiryKind ExpiryKind,
    DateOnly? OpenedOn,
    DateOnly? FrozenOn,
    DateOnly? ThawedOn,
    DateOnly? PreparedOn)
{
    /// <summary>Captures the current state of <paramref name="batch"/>.</summary>
    public static StockBatchSnapshot From(StockBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return new StockBatchSnapshot(
            batch.Id,
            batch.Quantity,
            batch.StorageType,
            batch.State,
            batch.PackageExpiresOn,
            batch.PackageExpiryKind,
            batch.ExpiresOn,
            batch.ExpiryKind,
            batch.OpenedOn,
            batch.FrozenOn,
            batch.ThawedOn,
            batch.PreparedOn);
    }

    /// <summary>Checks the invariants every stored batch fulfils.</summary>
    /// <exception cref="ArgumentException">The snapshot is inconsistent.</exception>
    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", nameof(Id));
        }

        // default(Quantity) has no unit; a used-up batch keeps its unit with amount zero.
        if (Quantity.Unit is null || Quantity.Value < 0m)
        {
            throw new ArgumentException("The quantity needs a unit and a non-negative amount.", nameof(Quantity));
        }

        if (PackageExpiresOn.HasValue == (PackageExpiryKind == ExpiryKind.None))
        {
            throw new ArgumentException("A package expiry kind is required exactly when a package date is given.", nameof(PackageExpiryKind));
        }

        if (ExpiresOn.HasValue == (ExpiryKind == ExpiryKind.None))
        {
            throw new ArgumentException("An expiry kind is required exactly when an expiry date is given.", nameof(ExpiryKind));
        }
    }
}

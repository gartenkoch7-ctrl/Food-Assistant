using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Infrastructure.Inventory;

/// <summary>
/// Maps between <see cref="StockBatchRecord"/> and the domain <see cref="StockBatch"/> via
/// <see cref="StockBatchSnapshot"/> (M1: the domain stays free of persistence).
/// </summary>
internal static class StockBatchMapping
{
    /// <summary>Rebuilds the domain batch; throws if the stored state violates the domain invariants.</summary>
    public static StockBatch ToDomain(this StockBatchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return StockBatch.Restore(new StockBatchSnapshot(
            record.Id,
            new Quantity(record.QuantityValue, Unit.FromCode(record.QuantityUnitCode)),
            record.StorageType,
            record.State,
            record.PackageExpiresOn,
            record.PackageExpiryKind,
            record.ExpiresOn,
            record.ExpiryKind,
            record.OpenedOn,
            record.FrozenOn,
            record.ThawedOn,
            record.PreparedOn));
    }

    /// <summary>Copies the state of <paramref name="batch"/> into the record and stamps <see cref="StockBatchRecord.UpdatedAt"/>.</summary>
    /// <exception cref="ArgumentException">The batch is a different one than the record.</exception>
    public static void Apply(this StockBatchRecord record, StockBatch batch, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        var snapshot = StockBatchSnapshot.From(batch);
        if (snapshot.Id != record.Id)
        {
            throw new ArgumentException("The batch does not belong to this record.", nameof(batch));
        }

        record.QuantityValue = snapshot.Quantity.Value;
        record.QuantityUnitCode = snapshot.Quantity.Unit.Code;
        record.StorageType = snapshot.StorageType;
        record.State = snapshot.State;
        record.PackageExpiresOn = snapshot.PackageExpiresOn;
        record.PackageExpiryKind = snapshot.PackageExpiryKind;
        record.ExpiresOn = snapshot.ExpiresOn;
        record.ExpiryKind = snapshot.ExpiryKind;
        record.OpenedOn = snapshot.OpenedOn;
        record.FrozenOn = snapshot.FrozenOn;
        record.ThawedOn = snapshot.ThawedOn;
        record.PreparedOn = snapshot.PreparedOn;
        record.UpdatedAt = now;
    }

    /// <summary>A new record for a batch that is not stored yet.</summary>
    public static StockBatchRecord NewRecord(
        StockBatch batch,
        Guid householdId,
        Guid productId,
        Guid storageLocationId,
        BatchOrigin origin,
        DateOnly addedOn,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var record = new StockBatchRecord
        {
            Id = batch.Id,
            HouseholdId = householdId,
            ProductId = productId,
            StorageLocationId = storageLocationId,
            QuantityUnitCode = batch.Quantity.Unit.Code,
            Origin = origin,
            AddedOn = addedOn,
            CreatedAt = now,
        };
        record.Apply(batch, now);
        return record;
    }
}

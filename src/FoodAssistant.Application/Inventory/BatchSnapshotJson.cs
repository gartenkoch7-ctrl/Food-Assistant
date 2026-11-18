using System.Text.Json;
using System.Text.Json.Serialization;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Inventory;

/// <summary>
/// Undo payload of an inventory activity (<c>HouseholdActivities.PayloadJson</c>). Own primitive
/// format (unit as code, dates as <c>yyyy-MM-dd</c>, enums as names) so the stored JSON does not
/// change when domain types are refactored. Never logged or shown.
/// </summary>
public static class BatchSnapshotJson
{
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectNullableAnnotations = true,
    };

    public static string Serialize(IReadOnlyList<BatchUndoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var payload = new Payload(FormatVersion, [.. entries.Select(ToStored)]);
        return JsonSerializer.Serialize(payload, Options);
    }

    /// <exception cref="FormatException">The payload is not in this format or inconsistent.</exception>
    public static IReadOnlyList<BatchUndoEntry> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json, Options);
            if (payload is null || payload.V != FormatVersion)
            {
                throw new FormatException("Unknown undo payload format.");
            }

            return [.. payload.Batches.Select(FromStored)];
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new FormatException("The undo payload is invalid.", ex);
        }
    }

    private static StoredEntry ToStored(BatchUndoEntry entry) => new(
        entry.BatchId,
        entry.UpdatedAtAfter,
        entry.EventId,
        entry.Before is { } s
            ? new StoredSnapshot(
                s.Quantity.Value, s.Quantity.Unit.Code, s.StorageType, s.State,
                s.PackageExpiresOn, s.PackageExpiryKind, s.ExpiresOn, s.ExpiryKind,
                s.OpenedOn, s.FrozenOn, s.ThawedOn, s.PreparedOn)
            : null,
        entry.LocationBefore is { } l
            ? new StoredLocation(l.ProductId, l.StorageLocationId, l.Origin, l.AddedOn)
            : null);

    private static BatchUndoEntry FromStored(StoredEntry entry)
    {
        StockBatchSnapshot? before = null;
        if (entry.Before is { } s)
        {
            before = new StockBatchSnapshot(
                entry.BatchId, new Quantity(s.Quantity, Unit.FromCode(s.Unit)), s.StorageType, s.State,
                s.PackageExpiresOn, s.PackageExpiryKind, s.ExpiresOn, s.ExpiryKind,
                s.OpenedOn, s.FrozenOn, s.ThawedOn, s.PreparedOn);
            before.Validate();
        }

        var location = entry.Location is { } l
            ? new BatchLocationInfo(l.ProductId, l.StorageLocationId, l.Origin, l.AddedOn)
            : null;
        if ((before is null) != (location is null))
        {
            throw new FormatException("State and placement belong together.");
        }

        return new BatchUndoEntry(entry.BatchId, before, location, entry.UpdatedAtAfter, entry.EventId);
    }

    private sealed record Payload(int V, IReadOnlyList<StoredEntry> Batches);

    private sealed record StoredEntry(
        Guid BatchId,
        DateTimeOffset? UpdatedAtAfter,
        Guid? EventId,
        StoredSnapshot? Before,
        StoredLocation? Location);

    private sealed record StoredSnapshot(
        decimal Quantity,
        string Unit,
        StorageType StorageType,
        BatchState State,
        DateOnly? PackageExpiresOn,
        ExpiryKind PackageExpiryKind,
        DateOnly? ExpiresOn,
        ExpiryKind ExpiryKind,
        DateOnly? OpenedOn,
        DateOnly? FrozenOn,
        DateOnly? ThawedOn,
        DateOnly? PreparedOn);

    private sealed record StoredLocation(Guid ProductId, Guid StorageLocationId, BatchOrigin Origin, DateOnly AddedOn);
}

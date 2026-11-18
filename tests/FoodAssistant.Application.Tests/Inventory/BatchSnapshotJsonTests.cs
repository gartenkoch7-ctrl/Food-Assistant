using FoodAssistant.Application.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Tests.Inventory;

/// <summary>The undo payload round-trips and rejects anything that is not its format.</summary>
public sealed class BatchSnapshotJsonTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        var batch = new StockBatch(Guid.CreateVersion7(), new Quantity(1.5m, Unit.Kilogram), StorageType.Chilled, Today.AddDays(4), ExpiryKind.BestBefore);
        batch.Open(Today, new ShelfLifeRule { DaysAfterOpening = 2 });
        var location = new BatchLocationInfo(Guid.CreateVersion7(), Guid.CreateVersion7(), BatchOrigin.Scanned, Today);
        var updatedAt = new DateTimeOffset(2026, 11, 18, 9, 30, 1, TimeSpan.Zero).AddTicks(1230);
        var deletedId = Guid.CreateVersion7();
        BatchUndoEntry[] entries =
        [
            new(batch.Id, StockBatchSnapshot.From(batch), location, updatedAt, Guid.CreateVersion7()),
            new(Guid.CreateVersion7(), null, null, updatedAt, null),
            new(deletedId, StockBatchSnapshot.From(batch) with { Id = deletedId }, location, null, null),
        ];

        var json = BatchSnapshotJson.Serialize(entries);
        var back = BatchSnapshotJson.Deserialize(json);

        Assert.Equal(entries, back);
        Assert.Contains("\"unit\":\"kg\"", json, StringComparison.Ordinal);
        Assert.Contains("\"openedOn\":\"2026-11-18\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"Opened\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"v\":2,\"batches\":[]}")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"batches\":[{\"batchId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"before\":{\"quantity\":1,\"unit\":\"bucket\",\"storageType\":\"Chilled\",\"state\":\"Sealed\",\"packageExpiryKind\":\"None\",\"expiryKind\":\"None\"},\"location\":{\"productId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"storageLocationId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"origin\":\"Manual\",\"addedOn\":\"2026-11-18\"}}]}")]
    [InlineData("{\"v\":1,\"batches\":[{\"batchId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"location\":{\"productId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"storageLocationId\":\"01a1eed6-3ab8-796d-8017-dc6d7066fd86\",\"origin\":\"Manual\",\"addedOn\":\"2026-11-18\"}}]}")]
    public void Deserialize_Invalid_ThrowsFormatException(string json) =>
        Assert.Throws<FormatException>(() => BatchSnapshotJson.Deserialize(json));
}

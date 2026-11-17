using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Domain.Tests.Inventory;

/// <summary>Builders for batches used across the inventory tests.</summary>
internal static class TestBatches
{
    public static readonly DateOnly Today = new(2026, 11, 18);

    public static readonly ShelfLifeRule FullRule = new()
    {
        DaysAfterOpening = 3,
        DaysFrozen = 90,
        DaysAfterThawing = 1,
        DaysAsLeftover = 2,
    };

    public static StockBatch Sealed(
        DateOnly? packageExpiresOn,
        ExpiryKind kind = ExpiryKind.BestBefore,
        decimal amount = 1m,
        Unit? unit = null,
        StorageType storage = StorageType.Chilled) =>
        new(
            Guid.NewGuid(),
            new Quantity(amount, unit ?? Unit.Piece),
            storage,
            packageExpiresOn,
            packageExpiresOn.HasValue ? kind : ExpiryKind.None);

    public static StockBatch SealedInDays(int days, decimal amount = 1m, Unit? unit = null) =>
        Sealed(Today.AddDays(days), ExpiryKind.BestBefore, amount, unit);
}

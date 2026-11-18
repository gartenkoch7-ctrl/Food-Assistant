using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Sync;

namespace FoodAssistant.Infrastructure.Tests.Sync;

/// <summary>Every synchronised entity needs a stable change-log name (no database needed).</summary>
public sealed class SyncEntityTypeMapTests
{
    private static readonly string[] KnownKeys =
    [
        SyncEntityTypes.StorageLocation,
        SyncEntityTypes.Product,
        SyncEntityTypes.Batch,
        SyncEntityTypes.ShoppingList,
        SyncEntityTypes.ShoppingItem,
        SyncEntityTypes.Store,
    ];

    [Fact]
    public async Task EverySyncTrackedEntityInTheModel_HasADistinctKnownKey()
    {
        // Building the model needs no server; the connection string is never opened.
        await using var database = await TestDatabase.CreateAsync("Host=localhost;Database=model_only", migrate: false);
        await using var scope = database.CreateScope();

        var tracked = scope.Db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(typeof(ISyncTracked).IsAssignableFrom)
            .ToList();
        var keys = tracked.Select(SyncEntityTypeMap.KeyOf).ToList();

        Assert.Equal(6, tracked.Count);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Contains(key, KnownKeys));
    }

    [Fact]
    public void UnknownType_Throws() =>
        Assert.Throws<InvalidOperationException>(() => SyncEntityTypeMap.KeyOf(typeof(ChangeLogEntry)));
}

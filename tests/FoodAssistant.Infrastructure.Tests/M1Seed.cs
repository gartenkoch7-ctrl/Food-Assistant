using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Inventory;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Shopping;
using FoodAssistant.Infrastructure.Stores;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Writes M1 test data directly through the context (no use cases involved).</summary>
internal static class M1Seed
{
    public static Task<StorageLocation> StorageLocationAsync(
        AppDbContext db,
        Guid householdId,
        string name = "Fridge",
        StorageType type = StorageType.Chilled) =>
        AddAsync(db, new StorageLocation
        {
            HouseholdId = householdId,
            Name = name,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public static Task<Product> ProductAsync(
        AppDbContext db,
        Guid householdId,
        string name = "Milk",
        string categoryKey = ProductCategories.Dairy,
        params string[] barcodes)
    {
        var product = new Product
        {
            HouseholdId = householdId,
            Name = name,
            CategoryKey = categoryKey,
            DefaultUnitCode = Unit.Litre.Code,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        foreach (var code in barcodes)
        {
            product.Barcodes.Add(new ProductBarcode { HouseholdId = householdId, ProductId = product.Id, Code = code });
        }

        return AddAsync(db, product);
    }

    public static Task<StockBatchRecord> BatchAsync(
        AppDbContext db,
        Guid householdId,
        Guid productId,
        Guid storageLocationId,
        DateOnly? expiresOn = null)
    {
        var batch = new StockBatch(
            Guid.CreateVersion7(),
            new Quantity(1m, Unit.Litre),
            StorageType.Chilled,
            expiresOn,
            expiresOn.HasValue ? ExpiryKind.BestBefore : ExpiryKind.None);
        var record = StockBatchMapping.NewRecord(
            batch,
            householdId,
            productId,
            storageLocationId,
            BatchOrigin.Manual,
            new DateOnly(2026, 11, 18),
            DateTimeOffset.UtcNow);
        return AddAsync(db, record);
    }

    public static Task<ShoppingList> ShoppingListAsync(AppDbContext db, Guid householdId, bool isDefault = true, string name = "") =>
        AddAsync(db, new ShoppingList
        {
            HouseholdId = householdId,
            Name = name,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public static Task<ShoppingItem> ShoppingItemAsync(
        AppDbContext db,
        Guid householdId,
        Guid listId,
        string text = "Bread",
        Guid? productId = null) =>
        AddAsync(db, new ShoppingItem
        {
            HouseholdId = householdId,
            ListId = listId,
            ProductId = productId,
            Text = text,
            SectionKey = StoreSections.Bakery,
            Origin = ShoppingItemOrigin.Manual,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public static Task<Store> StoreAsync(AppDbContext db, Guid householdId, string name = "Market") =>
        AddAsync(db, new Store
        {
            HouseholdId = householdId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    private static async Task<T> AddAsync<T>(AppDbContext db, T entity)
        where T : class
    {
        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }
}

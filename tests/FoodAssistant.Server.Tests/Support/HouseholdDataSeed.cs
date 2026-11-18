using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
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
using FoodAssistant.Server.Households;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Support;

/// <summary>
/// Writes household data for API tests directly through <see cref="AppDbContext"/> of the test
/// server (<c>app.Services</c>), each call in its own scope with the household activated.
/// </summary>
internal static class HouseholdDataSeed
{
    public static Task<StorageLocation> StorageLocationAsync(
        IServiceProvider services,
        Guid householdId,
        string name = "Fridge",
        StorageType type = StorageType.Chilled) =>
        AddAsync(services, householdId, new StorageLocation
        {
            HouseholdId = householdId,
            Name = name,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public static Task<Product> ProductAsync(
        IServiceProvider services,
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

        return AddAsync(services, householdId, product);
    }

    /// <summary>A sealed batch of 1 l; without <paramref name="expiresOn"/> it has no date.</summary>
    public static Task<StockBatchRecord> BatchAsync(
        IServiceProvider services,
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
        var now = DateTimeOffset.UtcNow;
        var record = StockBatchMapping.NewRecord(
            batch,
            householdId,
            productId,
            storageLocationId,
            BatchOrigin.Manual,
            DateOnly.FromDateTime(now.UtcDateTime),
            now);
        return AddAsync(services, householdId, record);
    }

    public static Task<ShoppingList> ShoppingListAsync(
        IServiceProvider services,
        Guid householdId,
        bool isDefault = true,
        string name = "") =>
        AddAsync(services, householdId, new ShoppingList
        {
            HouseholdId = householdId,
            Name = name,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public static Task<ShoppingItem> ShoppingItemAsync(
        IServiceProvider services,
        Guid householdId,
        Guid listId,
        string text = "Bread",
        Guid? productId = null) =>
        AddAsync(services, householdId, new ShoppingItem
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

    public static Task<Store> StoreAsync(IServiceProvider services, Guid householdId, string name = "Market") =>
        AddAsync(services, householdId, new Store
        {
            HouseholdId = householdId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    private static async Task<T> AddAsync<T>(IServiceProvider services, Guid householdId, T entity)
        where T : class
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }
}

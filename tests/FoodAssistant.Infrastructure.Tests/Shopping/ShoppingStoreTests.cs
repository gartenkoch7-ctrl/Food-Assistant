using FoodAssistant.Application.Shopping;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Tests.Shopping;

/// <summary>Shopping stores against PostgreSQL: default list, tombstones and the item version counter.</summary>
public sealed class ShoppingStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnsureDefaultList_Concurrently_CreatesExactlyOne()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var householdId = await SeedHouseholdAsync(database);

        var lists = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = database.CreateScope(householdId);
            return await scope.Get<IShoppingListStore>().EnsureDefaultListAsync(DateTimeOffset.UtcNow, Ct);
        }));

        Assert.Single(lists.Select(l => l.Id).Distinct());
        await using var check = database.CreateScope(householdId);
        var stored = Assert.Single(await check.Db.Set<ShoppingList>().ToListAsync(Ct));
        Assert.True(stored.IsDefault);
        Assert.Equal(string.Empty, stored.Name);
    }

    [Fact]
    public async Task Tombstones_OnlyVisibleWhenAsked_AndNeverForAnotherHousehold()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var householdId = await SeedHouseholdAsync(database);
        var otherHouseholdId = await SeedHouseholdAsync(database, "other");
        Guid listId, deletedId;
        await using (var seed = database.CreateScope(householdId))
        {
            listId = (await M1Seed.ShoppingListAsync(seed.Db, householdId)).Id;
            var deleted = await M1Seed.ShoppingItemAsync(seed.Db, householdId, listId);
            deleted.DeletedAt = DateTimeOffset.UtcNow;
            await seed.Db.SaveChangesAsync(Ct);
            deletedId = deleted.Id;
        }

        await using var scope = database.CreateScope(householdId);
        var items = scope.Get<IShoppingItemStore>();
        Assert.Null(await items.GetItemAsync(deletedId, includeDeleted: false, Ct));
        Assert.True((await items.GetItemAsync(deletedId, includeDeleted: true, Ct))!.Deleted);
        Assert.Empty(await items.ListItemsAsync(listId, includeChecked: true, Ct));

        await using var foreign = database.CreateScope(otherHouseholdId);
        Assert.Null(await foreign.Get<IShoppingItemStore>().GetItemAsync(deletedId, includeDeleted: true, Ct));
    }

    [Fact]
    public async Task ApplyItem_StaleExpectedVersion_SavesNothing()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var householdId = await SeedHouseholdAsync(database);
        Guid itemId;
        await using (var seed = database.CreateScope(householdId))
        {
            var list = await M1Seed.ShoppingListAsync(seed.Db, householdId);
            itemId = (await M1Seed.ShoppingItemAsync(seed.Db, householdId, list.Id)).Id;
        }

        await using var first = database.CreateScope(householdId);
        await using var second = database.CreateScope(householdId);
        var original = (await first.Get<IShoppingItemStore>().GetItemAsync(itemId, includeDeleted: false, Ct))!;

        var won = await first.Get<IShoppingItemStore>().ApplyItemAsync(
            original with { Text = "Rolls", Version = 2 }, expectedVersion: 1, Ct);
        var lost = await second.Get<IShoppingItemStore>().ApplyItemAsync(
            original with { Text = "Cake", Version = 2 }, expectedVersion: 1, Ct);

        Assert.True(won);
        Assert.False(lost);
        await using var check = database.CreateScope(householdId);
        var stored = (await check.Get<IShoppingItemStore>().GetItemAsync(itemId, includeDeleted: false, Ct))!;
        Assert.Equal(("Rolls", 2L), (stored.Text, stored.Version));
    }

    [Fact]
    public async Task AddItem_IdOfAnotherHousehold_ReturnsNullAndSavesNothing()
    {
        await using var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString());
        var householdId = await SeedHouseholdAsync(database);
        var otherHouseholdId = await SeedHouseholdAsync(database, "other");
        Guid takenId, otherListId;
        await using (var seed = database.CreateScope(householdId))
        {
            var list = await M1Seed.ShoppingListAsync(seed.Db, householdId);
            takenId = (await M1Seed.ShoppingItemAsync(seed.Db, householdId, list.Id)).Id;
        }

        await using (var seed = database.CreateScope(otherHouseholdId))
        {
            otherListId = (await M1Seed.ShoppingListAsync(seed.Db, otherHouseholdId)).Id;
        }

        await using var scope = database.CreateScope(otherHouseholdId);
        var added = await scope.Get<IShoppingItemStore>().AddItemAsync(
            otherListId,
            new NewShoppingItem(takenId, null, "Milk", null, null, "other", Domain.Shopping.ShoppingItemOrigin.Manual, null),
            DateTimeOffset.UtcNow,
            Ct);

        Assert.Null(added);
        Assert.Empty(await scope.Get<IShoppingItemStore>().ListItemsAsync(otherListId, includeChecked: true, Ct));
    }

    private static async Task<Guid> SeedHouseholdAsync(TestDatabase database, string owner = "owner")
    {
        await using var seed = database.CreateScope();
        var user = await Seed.UserAsync(seed.Db, owner);
        return (await Seed.HouseholdAsync(seed.Db, user.Id)).Id;
    }
}

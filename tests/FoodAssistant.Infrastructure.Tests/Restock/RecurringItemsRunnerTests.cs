using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Restock;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Households;
using FoodAssistant.Infrastructure.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Infrastructure.Tests.Restock;

/// <summary>The recurring run against PostgreSQL with a fixed clock (household time zone: Europe/Berlin).</summary>
public sealed class RecurringItemsRunnerTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DueRule_AddsOneRecurringEntry_AndMovesTheDueDateOn()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        var ruleId = await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 18), RecurrenceFrequency.Daily);

        Assert.Equal(1, await RunAsync(database, time, household));
        Assert.Equal(0, await RunAsync(database, time, household));

        var item = Assert.Single(await ItemsAsync(database, household));
        Assert.Equal(("Milk", ShoppingItemOrigin.Recurring, listId), (item.Text, item.Origin, item.ListId));
        var rule = await RuleAsync(database, household, ruleId);
        Assert.Equal((new DateOnly(2026, 11, 19), item.Id), (rule.NextDueOn, rule.LastCreatedItemId));
    }

    [Fact]
    public async Task RuleNotDueYet_AddsNothing()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 19), RecurrenceFrequency.Daily);

        Assert.Equal(0, await RunAsync(database, time, household));
        Assert.Empty(await ItemsAsync(database, household));
    }

    [Fact]
    public async Task LastEntryStillOpen_SkipsButMovesOn_UntilItIsCheckedOrDeleted()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        var ruleId = await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 18), RecurrenceFrequency.Daily);
        await RunAsync(database, time, household);

        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await RunAsync(database, time, household));
        Assert.Single(await ItemsAsync(database, household));
        Assert.Equal(new DateOnly(2026, 11, 20), (await RuleAsync(database, household, ruleId)).NextDueOn);

        await using (var scope = database.CreateScope(household))
        {
            var open = await scope.Db.Set<ShoppingItem>().SingleAsync(Ct);
            open.Checked = true;
            await scope.Db.SaveChangesAsync(Ct);
        }

        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, await RunAsync(database, time, household));
        Assert.Equal(2, (await ItemsAsync(database, household)).Count);
    }

    [Fact]
    public async Task LastEntryDeleted_CountsAsDone()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 18), RecurrenceFrequency.Daily);
        await RunAsync(database, time, household);
        await using (var scope = database.CreateScope(household))
        {
            var open = await scope.Db.Set<ShoppingItem>().SingleAsync(Ct);
            open.DeletedAt = Noon;
            await scope.Db.SaveChangesAsync(Ct);
        }

        time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, await RunAsync(database, time, household));
    }

    [Fact]
    public async Task Today_FollowsTheHouseholdTimeZone()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 19), RecurrenceFrequency.Daily);

        // 23:30 UTC on the 18th is already the 19th in Berlin.
        time.Set(new DateTimeOffset(2026, 11, 18, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(1, await RunAsync(database, time, household));
    }

    [Fact]
    public async Task OtherHouseholdsRules_AreNotTouched()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        Guid otherHousehold, otherListId;
        await using (var seed = database.CreateScope())
        {
            var user = await Seed.UserAsync(seed.Db, "other");
            otherHousehold = (await Seed.HouseholdAsync(seed.Db, user.Id)).Id;
        }

        await using (var seed = database.CreateScope(otherHousehold))
        {
            otherListId = (await M1Seed.ShoppingListAsync(seed.Db, otherHousehold)).Id;
        }

        await AddRuleAsync(database, otherHousehold, otherListId, new DateOnly(2026, 11, 18), RecurrenceFrequency.Daily);

        Assert.Equal(0, await RunAsync(database, time, household));
        Assert.Empty(await ItemsAsync(database, otherHousehold));
    }

    [Fact]
    public async Task ProductRule_UsesProductNameAndSection_AndFallsBackToTextWhenArchived()
    {
        var (database, time, household, listId) = await StartAsync();
        await using var _ = database;
        Guid productId;
        await using (var seed = database.CreateScope(household))
        {
            productId = (await M1Seed.ProductAsync(seed.Db, household, "Butter")).Id;
        }

        await AddRuleAsync(database, household, listId, new DateOnly(2026, 11, 18), RecurrenceFrequency.Daily, productId, "Old name");
        await RunAsync(database, time, household);
        await using (var scope = database.CreateScope(household))
        {
            var product = await scope.Db.Set<FoodAssistant.Infrastructure.Catalog.Product>().SingleAsync(Ct);
            product.ArchivedAt = Noon;
            await scope.Db.SaveChangesAsync(Ct);
        }

        time.Advance(TimeSpan.FromDays(1));
        await using (var scope = database.CreateScope(household))
        {
            var first = await scope.Db.Set<ShoppingItem>().SingleAsync(Ct);
            first.Checked = true;
            await scope.Db.SaveChangesAsync(Ct);
        }

        await RunAsync(database, time, household);

        var items = (await ItemsAsync(database, household)).OrderBy(i => i.CreatedAt).ToList();
        Assert.Equal(("Butter", productId), (items[0].Text, items[0].ProductId));
        Assert.Equal(("Old name", (Guid?)null), (items[1].Text, items[1].ProductId));
    }

    private async Task<(TestDatabase Database, ManualTimeProvider Time, Guid Household, Guid ListId)> StartAsync()
    {
        var time = new ManualTimeProvider();
        time.Set(Noon);
        var database = await TestDatabase.CreateAsync(postgres.NewDatabaseConnectionString(), time: time);
        await using var seed = database.CreateScope();
        var user = await Seed.UserAsync(seed.Db, "owner");
        var household = (await Seed.HouseholdAsync(seed.Db, user.Id)).Id;
        await using var listScope = database.CreateScope(household);
        var list = await M1Seed.ShoppingListAsync(listScope.Db, household);
        return (database, time, household, list.Id);
    }

    private static async Task<Guid> AddRuleAsync(
        TestDatabase database,
        Guid household,
        Guid listId,
        DateOnly dueOn,
        RecurrenceFrequency frequency,
        Guid? productId = null,
        string text = "Milk")
    {
        await using var scope = database.CreateScope(household);
        var rule = new RecurringShoppingRule
        {
            HouseholdId = household,
            ListId = listId,
            ProductId = productId,
            Text = text,
            Frequency = frequency,
            NextDueOn = dueOn,
            CreatedAt = Noon,
            UpdatedAt = Noon,
        };
        scope.Db.Add(rule);
        await scope.Db.SaveChangesAsync(Ct);
        return rule.Id;
    }

    private static async Task<int> RunAsync(TestDatabase database, ManualTimeProvider time, Guid household)
    {
        await using var scope = database.CreateScope(household);
        var itemStore = scope.Get<IShoppingItemStore>();
        var context = scope.Get<IHouseholdContext>();
        var items = new ShoppingItemService(itemStore, scope.Get<IShoppingListStore>(), context, scope.Get<IActivityLog>(), time);
        var runner = new RecurringItemsRunner(
            scope.Get<IRecurringRuleStore>(),
            itemStore,
            items,
            scope.Get<IHouseholdCalendar>(),
            context,
            time,
            NullLogger<RecurringItemsRunner>.Instance);
        return await runner.RunAsync(Ct);
    }

    private static async Task<List<ShoppingItem>> ItemsAsync(TestDatabase database, Guid household)
    {
        await using var scope = database.CreateScope(household);
        return await scope.Db.Set<ShoppingItem>().AsNoTracking().ToListAsync(Ct);
    }

    private static async Task<RecurringShoppingRule> RuleAsync(TestDatabase database, Guid household, Guid ruleId)
    {
        await using var scope = database.CreateScope(household);
        return await scope.Db.Set<RecurringShoppingRule>().AsNoTracking().SingleAsync(r => r.Id == ruleId, Ct);
    }
}

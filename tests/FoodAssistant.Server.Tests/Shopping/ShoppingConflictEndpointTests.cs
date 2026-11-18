using System.Net;
using System.Text.Json;
using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Activity;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Server.Tests.Shopping;

/// <summary>
/// Every row of the conflict table of ADR-0005 through the API: a second device sends a change
/// based on version 1 after the first device already changed the entry.
/// </summary>
public sealed class ShoppingConflictEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly QuantityDto Two = new(2m, UnitCodes.Piece);
    private static readonly QuantityDto Three = new(3m, UnitCodes.Piece);

    [Fact]
    public async Task CheckedOnServer_StaleChange_CheckedWins()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var stale = await api.AddTextAsync((await api.DefaultListAsync()).Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Checked = true });

        var result = await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two });

        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWins, result.Outcome);
        Assert.Equal((true, (QuantityDto?)null, 2L), (result.Item.Checked, result.Item.Quantity, result.Item.Version));
    }

    [Fact]
    public async Task ChangedOnServer_StaleCheck_CheckedWinsWithItsValues()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var stale = await api.AddTextAsync((await api.DefaultListAsync()).Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two, Note = "brown" });

        var result = await api.ChangeOkAsync(api.Owner, stale, c => c with { Checked = true });

        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWins, result.Outcome);
        Assert.Equal((true, (QuantityDto?)null, (string?)null, 3L), (result.Item.Checked, result.Item.Quantity, result.Item.Note, result.Item.Version));
        Assert.NotNull(result.Item.CheckedAt);
    }

    [Fact]
    public async Task TwoNewEntriesForTheSameArticle_BothAreKept()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();

        var first = await api.AddTextAsync(list.Id, "Milk");
        var second = await api.AddTextAsync(list.Id, "Milk");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await api.ItemsAsync(list.Id)).Count(i => i.Text == "Milk"));
    }

    [Fact]
    public async Task QuantityChangedTwice_TheLastReceivedChangeWins()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var stale = await api.AddTextAsync((await api.DefaultListAsync()).Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two });
        api.App.Time.Advance(TimeSpan.FromSeconds(1));

        var result = await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Three, Text = "Rolls" });

        Assert.Equal(ShoppingConflictOutcomeDto.LastWriteWins, result.Outcome);
        Assert.Equal((Three, "Rolls", 3L), (result.Item.Quantity, result.Item.Text, result.Item.Version));
    }

    [Fact]
    public async Task DeletedOnServer_StaleChange_ChangeWinsAndEntryReturns()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var stale = await api.AddTextAsync(list.Id, "Bread");
        await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);

        var result = await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two });

        Assert.Equal(ShoppingConflictOutcomeDto.ChangeWinsOverDelete, result.Outcome);
        Assert.Equal(3L, result.Item.Version);
        Assert.Equal(Two, Assert.Single(await api.ItemsAsync(list.Id)).Quantity);
    }

    [Fact]
    public async Task ChangedOnServer_StaleDelete_ChangeWinsAndEntryStays()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var stale = await api.AddTextAsync(list.Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two });

        var result = await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);

        Assert.Equal(ShoppingConflictOutcomeDto.ChangeWinsOverDelete, result.Outcome);
        Assert.Equal(2L, result.Item.Version);
        Assert.Single(await api.ItemsAsync(list.Id));
    }

    [Fact]
    public async Task DeletedOnServer_StaleCheck_EntryStaysChecked()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var stale = await api.AddTextAsync(list.Id, "Bread");
        await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);

        var result = await api.ChangeOkAsync(api.Owner, stale, c => c with { Checked = true });

        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWinsOverDelete, result.Outcome);
        Assert.True(Assert.Single(await api.ItemsAsync(list.Id)).Checked);
    }

    [Fact]
    public async Task CheckedOnServer_StaleDelete_EntryStaysChecked()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var stale = await api.AddTextAsync(list.Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Checked = true });

        var result = await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);

        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWinsOverDelete, result.Outcome);
        Assert.True(Assert.Single(await api.ItemsAsync(list.Id)).Checked);
    }

    [Fact]
    public async Task DeletedTwice_StaysDeleted_AndRepeatedDeleteChangesNothing()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var stale = await api.AddTextAsync(list.Id, "Bread");
        var deleted = await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);

        var both = await api.DeleteOkAsync(api.Owner, stale.Id, stale.Version);
        var again = await api.DeleteOkAsync(api.Owner, stale.Id, deleted.Item.Version);

        Assert.Equal(ShoppingConflictOutcomeDto.NoConflict, deleted.Outcome);
        Assert.Equal(ShoppingConflictOutcomeDto.BothDeleted, both.Outcome);
        Assert.Equal((ShoppingConflictOutcomeDto.NoConflict, 2L), (again.Outcome, again.Item.Version));
        Assert.Empty(await api.ItemsAsync(list.Id));
    }

    [Fact]
    public async Task UnknownBaseVersion_Returns409()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var item = await api.AddTextAsync((await api.DefaultListAsync()).Id, "Bread");

        using var change = await api.ChangeAsync(api.Owner, ShoppingApi.ToChange(item) with { BaseVersion = 7 }, item.Id);
        using var delete = await api.Owner.DeleteAsync($"{api.Base}/items/{item.Id}?baseVersion=7");

        await TestClient.AssertProblemAsync(change, HttpStatusCode.Conflict, ShoppingErrorCodes.UnknownVersion);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Conflict, ShoppingErrorCodes.UnknownVersion);
    }

    [Fact]
    public async Task Conflicts_AreRecordedInTheActivityFeed_AlsoWhenTheServerKeepsItsState()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var stale = await api.AddTextAsync((await api.DefaultListAsync()).Id, "Bread");
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Checked = true });
        await api.ChangeOkAsync(api.Owner, stale, c => c with { Quantity = Two });

        var kinds = await api.WithDbAsync(db => db.Set<HouseholdActivity>()
            .Where(a => a.SubjectId == stale.Id && a.Area == ActivityArea.Shopping)
            .OrderBy(a => a.OccurredAt)
            .ThenBy(a => a.Id)
            .Select(a => new { a.Kind, a.PayloadJson })
            .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            [ShoppingActivityKinds.ItemAdded, ShoppingActivityKinds.ItemChecked, ShoppingActivityKinds.ConflictResolved],
            kinds.Select(k => k.Kind));
        using var payload = JsonDocument.Parse(kinds[^1].PayloadJson!);
        Assert.Equal("checked_wins", payload.RootElement.GetProperty("outcome").GetString());
    }
}

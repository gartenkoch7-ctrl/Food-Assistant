using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Domain.Tests.Shopping;

/// <summary>Conflict table of ADR-0005.</summary>
public sealed class ShoppingConflictResolverTests
{
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly DateTimeOffset Base = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static ShoppingItemState Item(
        long version,
        bool isChecked = false,
        decimal amount = 1m,
        string? note = null,
        bool deleted = false,
        int minutes = 0) =>
        new(ItemId, version, isChecked, new Quantity(amount, Unit.Piece), note, deleted, Base.AddMinutes(minutes));

    [Fact]
    public void Resolve_UnknownItem_IsCreated()
    {
        var incoming = Item(0, note: "new");

        var result = ShoppingConflictResolver.Resolve(null, incoming);

        Assert.Equal(ShoppingConflictOutcome.Created, result.Outcome);
        Assert.True(result.ServerChanged);
        Assert.Equal(incoming with { Version = 1 }, result.State);
    }

    [Fact]
    public void Resolve_TwoNewEntriesForSameArticle_BothAreKept()
    {
        var first = new ShoppingItemState(Guid.NewGuid(), 0, false, new Quantity(1m, Unit.Litre), "milk", false, Base);
        var second = first with { Id = Guid.NewGuid(), UpdatedAt = Base.AddMinutes(1) };

        var results = new[] { ShoppingConflictResolver.Resolve(null, first), ShoppingConflictResolver.Resolve(null, second) };

        Assert.All(results, r => Assert.Equal(ShoppingConflictOutcome.Created, r.Outcome));
        Assert.Equal(2, results.Select(r => r.State.Id).Distinct().Count());
    }

    [Fact]
    public void Resolve_CurrentVersion_AppliesWithoutConflict()
    {
        var incoming = Item(3, isChecked: false, amount: 4m, minutes: 5);

        var result = ShoppingConflictResolver.Resolve(Item(3, isChecked: true), incoming);

        Assert.Equal(ShoppingConflictOutcome.NoConflict, result.Outcome);
        Assert.Equal(incoming with { Version = 4 }, result.State);
        Assert.True(result.ServerChanged);
    }

    [Fact]
    public void Resolve_IncomingCheckedVsServerChange_CheckedWins()
    {
        var server = Item(5, amount: 3m, minutes: 1);
        var incoming = Item(4, isChecked: true, amount: 2m, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.CheckedWins, result.Outcome);
        Assert.Equal(incoming with { Version = 6 }, result.State);
    }

    [Fact]
    public void Resolve_ServerCheckedVsIncomingChange_CheckedStays()
    {
        var server = Item(5, isChecked: true, amount: 2m, minutes: 1);
        var incoming = Item(4, amount: 3m, note: "bigger pack", minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.CheckedWins, result.Outcome);
        Assert.Same(server, result.State);
        Assert.False(result.ServerChanged);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_BothChangedQuantity_LaterArrivalWins(bool bothChecked)
    {
        var server = Item(5, bothChecked, amount: 3m, minutes: 1);
        var incoming = Item(4, bothChecked, amount: 2m, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.LastWriteWins, result.Outcome);
        Assert.Equal(new Quantity(2m, Unit.Piece), result.State.Quantity);
        Assert.Equal(6, result.State.Version);
    }

    [Fact]
    public void Resolve_BothChangedNote_LaterArrivalWins()
    {
        var server = Item(5, note: "organic", minutes: 1);
        var incoming = Item(4, note: "cheapest", minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal("cheapest", result.State.Note);
    }

    [Fact]
    public void Resolve_ServerChangeArrivedLater_ServerWins()
    {
        var server = Item(5, note: "organic", minutes: 3);
        var incoming = Item(4, note: "cheapest", minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.LastWriteWins, result.Outcome);
        Assert.Same(server, result.State);
        Assert.False(result.ServerChanged);
    }

    [Fact]
    public void Resolve_SameArrivalTime_IncomingWins()
    {
        var server = Item(5, amount: 3m, minutes: 2);
        var incoming = Item(4, amount: 2m, minutes: 2);

        Assert.Equal(new Quantity(2m, Unit.Piece), ShoppingConflictResolver.Resolve(server, incoming).State.Quantity);
    }

    [Fact]
    public void Resolve_ServerDeletedVsIncomingChange_ChangeWinsAndItemStays()
    {
        var server = Item(5, deleted: true, minutes: 1);
        var incoming = Item(4, amount: 6m, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.ChangeWinsOverDelete, result.Outcome);
        Assert.False(result.State.Deleted);
        Assert.Equal(new Quantity(6m, Unit.Piece), result.State.Quantity);
        Assert.Equal(6, result.State.Version);
    }

    [Fact]
    public void Resolve_IncomingDeleteVsServerChange_ChangeWinsAndItemStays()
    {
        var server = Item(5, amount: 6m, minutes: 1);
        var incoming = Item(4, deleted: true, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.ChangeWinsOverDelete, result.Outcome);
        Assert.Same(server, result.State);
        Assert.False(result.ServerChanged);
    }

    [Fact]
    public void Resolve_ServerDeletedVsIncomingChecked_CheckedStays()
    {
        var server = Item(5, deleted: true, minutes: 1);
        var incoming = Item(4, isChecked: true, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.CheckedWinsOverDelete, result.Outcome);
        Assert.True(result.State.Checked);
        Assert.False(result.State.Deleted);
    }

    [Fact]
    public void Resolve_IncomingDeleteVsServerChecked_CheckedStays()
    {
        var server = Item(5, isChecked: true, minutes: 1);
        var incoming = Item(4, deleted: true, minutes: 2);

        var result = ShoppingConflictResolver.Resolve(server, incoming);

        Assert.Equal(ShoppingConflictOutcome.CheckedWinsOverDelete, result.Outcome);
        Assert.Same(server, result.State);
    }

    [Fact]
    public void Resolve_BothDeleted_StaysDeleted()
    {
        var server = Item(5, deleted: true, minutes: 1);

        var result = ShoppingConflictResolver.Resolve(server, Item(4, deleted: true, minutes: 2));

        Assert.Equal(ShoppingConflictOutcome.BothDeleted, result.Outcome);
        Assert.Same(server, result.State);
        Assert.False(result.ServerChanged);
    }

    [Fact]
    public void Resolve_DifferentItem_Throws() =>
        Assert.Throws<ArgumentException>(
            () => ShoppingConflictResolver.Resolve(Item(1), Item(1) with { Id = Guid.NewGuid() }));

    [Fact]
    public void Resolve_VersionFromFuture_Throws() =>
        Assert.Throws<ArgumentException>(() => ShoppingConflictResolver.Resolve(Item(1), Item(2)));

    [Fact]
    public void Resolve_NullIncoming_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ShoppingConflictResolver.Resolve(Item(1), null!));
}

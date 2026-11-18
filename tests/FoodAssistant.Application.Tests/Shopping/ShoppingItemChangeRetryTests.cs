using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Application.Stores;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Application.Tests.Shopping;

/// <summary>A save that loses a race is decided again from the fresh state, once.</summary>
public sealed class ShoppingItemChangeRetryTests
{
    private static readonly Guid HouseholdId = Guid.CreateVersion7();
    private static readonly Guid ItemId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Created = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static ShoppingItemRecord Open => new(
        ItemId, Guid.CreateVersion7(), null, "Bread", null, null, StoreSections.Bakery, ShoppingItemOrigin.Manual,
        false, null, null, 1, Created, null);

    [Fact]
    public async Task LostRace_SecondAttemptSeesTheConcurrentCheckOff()
    {
        // Someone checked the entry off between our read and our save.
        var store = new RacingItemStore(Open, Open with { Checked = true, CheckedAt = Created, Version = 2 }, losses: 1);

        var result = await Service(store).ChangeAsync(
            HouseholdId, ItemId, Change(new QuantityDto(2m, UnitCodes.Piece)), Guid.CreateVersion7(), HouseholdRole.Member, TestContext.Current.CancellationToken);

        Assert.Equal(OperationStatus.Ok, result.Status);
        Assert.Equal(ShoppingConflictOutcomeDto.CheckedWins, result.Value!.Outcome);
        Assert.True(result.Value.Item.Checked);
        Assert.Null(result.Value.Item.Quantity);
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task TwoLostRaces_AnswerConflict()
    {
        var store = new RacingItemStore(Open, Open, losses: 2);

        var result = await Service(store).ChangeAsync(
            HouseholdId, ItemId, Change(null), Guid.CreateVersion7(), HouseholdRole.Member, TestContext.Current.CancellationToken);

        Assert.Equal((OperationStatus.Conflict, ErrorCodes.Conflict), (result.Status, result.ErrorCode));
    }

    private static ChangeShoppingItemRequest Change(QuantityDto? quantity) =>
        new(1, false, quantity, null, "Bread", StoreSections.Bakery);

    private static ShoppingItemChangeService Service(IShoppingItemStore store) =>
        new(store, new NoStoreLists(), new Household(), new IgnoredActivity(), new NoLearning(), TimeProvider.System,
            NullLogger<ShoppingItemChangeService>.Instance);

    private sealed class RacingItemStore(ShoppingItemRecord first, ShoppingItemRecord afterRace, int losses) : IShoppingItemStore
    {
        private int _losses = losses;

        public int Reads { get; private set; }

        public Task<ShoppingItemRecord?> GetItemAsync(Guid itemId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<ShoppingItemRecord?>(Reads++ == 0 ? first : afterRace);

        public Task<bool> ApplyItemAsync(ShoppingItemRecord updated, long expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult(_losses-- <= 0);

        public Task SavePendingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ShoppingItemRecord>> ListItemsAsync(Guid listId, bool includeChecked, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ShoppingItemRecord?> AddItemAsync(Guid listId, NewShoppingItem item, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(string Name, string CategoryKey, string? SectionKey)?> GetProductForItemAsync(Guid productId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoStoreLists : IShoppingListStore
    {
        public Task<Guid?> GetStoreIdOfListAsync(Guid listId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<ShoppingListDto>> ListListsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ShoppingListDto?> GetListAsync(Guid listId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ListExistsAsync(Guid listId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OperationResult<ShoppingListDto>> AddListAsync(Guid id, string name, Guid? storeId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OperationResult<ShoppingListDto>> UpdateListAsync(Guid listId, long? expectedVersion, UpdateShoppingListRequest changes, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OperationResult<ShoppingListDto>> DeleteListAsync(Guid listId, long? expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ShoppingListDto> EnsureDefaultListAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> StoreExistsAsync(Guid storeId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Household : IHouseholdContext
    {
        public Guid? HouseholdId => ShoppingItemChangeRetryTests.HouseholdId;

        public HouseholdMembershipInfo? Membership => new(ShoppingItemChangeRetryTests.HouseholdId, Guid.Empty, HouseholdRole.Owner);
    }

    private sealed class IgnoredActivity : IActivityLog
    {
        public Guid Add(ActivityEntry entry) => Guid.CreateVersion7();
    }

    private sealed class NoLearning : ISectionOrderLearning
    {
        public Task RecordCheckAsync(Guid storeId, string sectionKey, DateTimeOffset checkedAt, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

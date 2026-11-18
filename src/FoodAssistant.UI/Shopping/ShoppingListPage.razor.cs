using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Catalog;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Shopping;

/// <summary>
/// One shopping list: items grouped by store section in the order of the list's store, checked items
/// collapsed under "In the cart". Reloads on live changes; check-offs of other members are announced.
/// </summary>
public sealed partial class ShoppingListPage
{
    private IReadOnlyList<ShoppingListDto> _lists = [];
    private ShoppingListDto? _list;
    private List<ShoppingItemDto> _items = [];
    private Dictionary<string, int> _position = PositionsOf(SectionKeys.All);
    private OwnItemChanges _own = default!;
    private ShoppingDialogs _dialogs = default!;
    private ShoppingListLive _live = default!;
    private ShoppingQuickAdd? _quickAdd;
    private ApiError? _error;
    private Guid _loadedFor;
    private int _generation;
    private bool _orderStale;
    private bool _announcePending;
    private bool _loading = true;
    private bool _cartOpen;

    [Parameter]
    public Guid ListId { get; set; }

    private string ListName => string.IsNullOrWhiteSpace(_list?.Name) ? T["DefaultListName"].Value : _list!.Name;

    private List<ShoppingItemDto> OpenItems => [.. _items.Where(i => !i.Checked)];

    private List<ShoppingItemDto> CheckedItems => [.. _items.Where(i => i.Checked).OrderByDescending(i => i.CheckedAt)];

    // Sections in the store's order; a section the order does not know (should not happen) goes last.
    private IEnumerable<IGrouping<string, ShoppingItemDto>> OpenGroups =>
        OpenItems.GroupBy(i => i.SectionKey).OrderBy(g => _position.GetValueOrDefault(g.Key, int.MaxValue));

    protected override void OnInitialized()
    {
        _own = new OwnItemChanges(Time);
        _dialogs = new ShoppingDialogs(Dialogs, T);
        _live = new ShoppingListLive(Live, () => Session.ActiveHouseholdId, RefreshAsync);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == ListId)
        {
            return;
        }

        // The component is reused when the route changes to another list: nothing of the old one may stay.
        _loadedFor = ListId;
        _loading = true;
        _error = null;
        _cartOpen = false;
        _items = [];
        (_list, _lists) = (null, []);
        await ReloadAsync(announce: false, reloadOrder: true);
    }

    private static Dictionary<string, int> PositionsOf(IReadOnlyList<string> order) =>
        order.Select((key, index) => (key, index)).ToDictionary(p => p.key, p => p.index);

    // The order only changes with the list's store or the store's own order, so it is fetched on first load,
    // after list/store changes and after a resync - not on every item change. Answers that come back after a
    // newer reload started (or after the route changed) are dropped, so what such a reload was asked to do
    // (fetch the order, announce check-offs) stays pending until a reload has really done it.
    private async Task ReloadAsync(bool announce, bool reloadOrder)
    {
        _orderStale |= reloadOrder;
        _announcePending |= announce;
        var generation = ++_generation;
        if (Session.ActiveHouseholdId is not { } household)
        {
            _loading = false;
            return;
        }

        var lists = await Shopping.ListListsAsync(household, CancellationToken.None);
        if (generation != _generation)
        {
            return;
        }

        _error = lists.Error;
        _lists = lists.Value ?? _lists;
        _list = lists.IsSuccess ? _lists.FirstOrDefault(l => l.Id == ListId) : _list;
        if (_list is not null)
        {
            var items = await Shopping.ListItemsAsync(household, ListId, true, CancellationToken.None);
            if (generation != _generation)
            {
                return;
            }

            _error ??= items.Error;
            if (items.Value is { } loaded)
            {
                if (_announcePending)
                {
                    AnnounceForeignCheckOffs(loaded);
                    _announcePending = false;
                }

                _items = [.. loaded];
            }

            if (_orderStale)
            {
                // Without an order the default one applies, so a failing store never hides the list.
                var order = await Stores.GetSectionOrderAsync(household, _list.StoreId, CancellationToken.None);
                if (generation != _generation)
                {
                    return;
                }

                _position = PositionsOf(order.Value?.Sections ?? SectionKeys.All);
                _orderStale = false;
            }
        }

        _loading = false;
    }

    private void AnnounceForeignCheckOffs(IReadOnlyList<ShoppingItemDto> loaded)
    {
        foreach (var item in _own.ForeignCheckOffs(_items, loaded))
        {
            Announcer.Announce(string.Format(CultureInfo.CurrentCulture, T["LiveChecked"].Value, item.Text));
        }
    }

    private void OnAdded(ShoppingItemDto item)
    {
        // An answer for the list the person has just left must not show up in this one.
        if (item.ListId == ListId && _items.All(i => i.Id != item.Id))
        {
            _items.Add(item);
        }
    }

    private void OnChanged(ShoppingItemChangeResult result)
    {
        // A late answer for the list the person just left belongs to neither view nor announcement.
        if (result.Item.ListId != ListId)
        {
            return;
        }

        _error = null;

        // A live reload may already have brought a newer state than this (older) response.
        var index = _items.FindIndex(i => i.Id == result.Item.Id);
        if (index >= 0 && result.Item.Version >= _items[index].Version)
        {
            _items[index] = result.Item;
        }

        // The row leaves its group (and the button the focus was on); the move between list and cart is told instead.
        Announcer.Announce(string.Format(CultureInfo.CurrentCulture, T[result.Item.Checked ? "ItemInCart" : "ItemBackOnList"].Value, result.Item.Text));
    }

    // Answers of rows of a list the person has already left are ignored.
    private void OnNotApplied(ShoppingItemDto item)
    {
        if (item.ListId == ListId)
        {
            Announcer.Announce(T["ChangeNotApplied"].Value);
        }
    }

    private async Task OnFailedAsync(ShoppingItemDto item, ApiError error)
    {
        if (item.ListId != ListId)
        {
            return;
        }

        await ReloadAsync(announce: false, reloadOrder: false);
        _error = error;
    }

    // The merge button vanishes with its hint, so the focus moves on to the field for the next item.
    private async Task OnMergedAsync(bool hintGone)
    {
        await ReloadAsync(announce: false, reloadOrder: false);
        if (hintGone && _quickAdd is not null)
        {
            await _quickAdd.FocusAsync();
        }
    }

    private async Task OpenItemAsync(ShoppingItemDto item)
    {
        if (await _dialogs.EditItemAsync(item, _lists))
        {
            await ReloadAsync(announce: false, reloadOrder: false);
        }
    }

    private async Task EditListAsync()
    {
        if (await _dialogs.EditListAsync(_list!))
        {
            await ReloadAsync(announce: false, reloadOrder: true);
        }
    }

    private async Task NewListAsync()
    {
        if (await _dialogs.CreateListAsync() is { } created)
        {
            Navigation.NavigateTo($"/shopping/{created.Id:D}");
        }
    }

    private Task RefreshAsync(bool announce, bool reloadOrder) => InvokeAsync(async () =>
    {
        await ReloadAsync(announce, reloadOrder);
        StateHasChanged();
    });

    public void Dispose() => _live.Dispose();
}

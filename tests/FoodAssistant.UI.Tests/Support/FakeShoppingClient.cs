using FoodAssistant.Client.Http;
using FoodAssistant.Client.Shopping;
using FoodAssistant.Contracts.Shopping;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Shopping client over in-memory lists. Versions increase on every change; a wrong ETag on a list
/// answers 412 like the server. Items are changed without ETag, like the real API.
/// </summary>
public sealed class FakeShoppingClient : IShoppingClient
{
    public List<ShoppingListDto> Lists { get; } = [];
    public List<ShoppingItemDto> Items { get; } = [];
    public List<string> Calls { get; } = [];
    /// <summary>The requests of <see cref="ChangeItemAsync"/> in order, to check what the UI sent.</summary>
    public List<ChangeShoppingItemRequest> Changes { get; } = [];
    /// <summary>The requests of <see cref="AddItemAsync"/> in order (also the failed ones), to check ids across retries.</summary>
    public List<CreateShoppingItemRequest> AddRequests { get; } = [];
    /// <summary>Names the fake gives to products that are added by id.</summary>
    public Dictionary<Guid, string> ProductNames { get; } = [];
    /// <summary>When set, every call fails with it.</summary>
    public ApiError? FailWith { get; set; }
    /// <summary>When set, only item changes (checking, editing, merging) fail with it.</summary>
    public ApiError? FailChangeWith { get; set; }
    /// <summary>When set, a change answers only once this completes (to look at the optimistic state).</summary>
    public TaskCompletionSource? HoldChange { get; set; }
    /// <summary>When set, a change is applied at once but answers only once this completes (the server publishes before it responds).</summary>
    public TaskCompletionSource? HoldResponse { get; set; }
    public TaskCompletionSource? HoldListItems { get; set; } // the next item read waits (a slow reload that a newer one overtakes)
    public bool FailChangeOnce { get; set; } // with FailChangeWith: only the first change fails
    public ApiError? FailUpdateListWith { get; set; } // only list updates fail
    public bool CreateConflictOnce { get; set; } // the next create succeeds on the server but answers 409
    public string? ConflictedName { get; set; } // with CreateConflictOnce: the list/store exists under this name
    public TaskCompletionSource? HoldAdd { get; set; } // the next add answers only when this completes
    /// <summary>The outcome reported for changes. Like the real resolver the incoming state wins, except for "checked wins" on an unchecked request (the server keeps its state).</summary>
    public ShoppingConflictOutcomeDto ChangeOutcome { get; set; } = ShoppingConflictOutcomeDto.NoConflict;
    public ApiError? FailDeleteWith { get; set; } // only item deletes fail
    /// <summary>The outcome the server reports for deletes; a lost delete leaves the item in place.</summary>
    public ShoppingConflictOutcomeDto DeleteOutcome { get; set; } = ShoppingConflictOutcomeDto.NoConflict;
    // A test that fails before it releases a hold must not hang the whole run.
    private static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(10);

    private static string ETagOf(long version) => FakeShoppingListRules.ETagOf(version);

    private ApiResult<T> Respond<T>(string call, Func<ApiResult<T>> answer)
    {
        Calls.Add(call);
        return FailWith is null ? answer() : ApiResult<T>.Failure(FailWith);
    }

    private static ApiError NotFound(string code) => FakeAccountClient.Error(404, code);

    private ShoppingListDto WithCounts(ShoppingListDto list) => list with
    {
        OpenItemCount = Items.Count(i => i.ListId == list.Id && !i.Checked),
        CheckedItemCount = Items.Count(i => i.ListId == list.Id && i.Checked),
    };

    public Task<ApiResult<IReadOnlyList<ShoppingListDto>>> ListListsAsync(Guid householdId, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("list-lists", () =>
            ApiResult<IReadOnlyList<ShoppingListDto>>.Success([.. Lists.OrderBy(l => l.SortOrder).Select(WithCounts)])));

    public Task<ApiResult<ShoppingListDto>> CreateListAsync(Guid householdId, CreateShoppingListRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("create-list", () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var list = new ShoppingListDto(request.Id ?? Guid.CreateVersion7(), request.Name, request.StoreId, false, Lists.Count, 0, 0, 1);
            list = CreateConflictOnce && ConflictedName is { } conflicted ? list with { Name = conflicted } : list;
            Lists.Add(list);
            if (CreateConflictOnce)
            {
                CreateConflictOnce = false; // the list exists, but the response is lost: a retry with the same id gets 409
                return ApiResult<ShoppingListDto>.Failure(FakeAccountClient.Error(409, "common.conflict"));
            }

            return ApiResult<ShoppingListDto>.Success(list, ETagOf(1));
        }));

    public Task<ApiResult<ShoppingListDto>> UpdateListAsync(
        Guid householdId, Guid listId, UpdateShoppingListRequest request, string etag, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("update-list:" + listId, () => FailUpdateListWith is not null
            ? ApiResult<ShoppingListDto>.Failure(FailUpdateListWith)
            : FakeShoppingListRules.Update(Lists, listId, request, etag)));

    public Task<ApiResult> DeleteListAsync(Guid householdId, Guid listId, string etag, CancellationToken cancellationToken)
    {
        Calls.Add("delete-list:" + listId);
        return Task.FromResult(FailWith is not null ? ApiResult.Failure(FailWith) : FakeShoppingListRules.Delete(Lists, Items, listId));
    }

    public async Task<ApiResult<IReadOnlyList<ShoppingItemDto>>> ListItemsAsync(
        Guid householdId, Guid listId, bool includeChecked, CancellationToken cancellationToken)
    {
        if (HoldListItems is { } hold)
        {
            HoldListItems = null; // only the first read waits
            await hold.Task.WaitAsync(HoldTimeout, cancellationToken).ConfigureAwait(false);
        }

        return Respond("list-items:" + listId, () =>
            ApiResult<IReadOnlyList<ShoppingItemDto>>.Success([.. Items.Where(i => i.ListId == listId && (includeChecked || !i.Checked))]));
    }

    public async Task<ApiResult<ShoppingItemDto>> AddItemAsync(
        Guid householdId, Guid listId, CreateShoppingItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (HoldAdd is { } hold)
        {
            HoldAdd = null;
            await hold.Task.WaitAsync(HoldTimeout, cancellationToken).ConfigureAwait(false);
        }

        AddRequests.Add(request);
        return Respond("add-item", () =>
        {
            if (Items.FirstOrDefault(i => i.Id == request.Id) is { } existing)
            {
                return ApiResult<ShoppingItemDto>.Success(existing);
            }

            var text = request.ProductId is { } product ? ProductNames.GetValueOrDefault(product, "Product") : request.Text!;
            var item = new ShoppingItemDto(
                request.Id ?? Guid.CreateVersion7(), listId, request.ProductId, text, request.Quantity, request.Note,
                request.SectionKey ?? "other", ShoppingItemOriginDto.Manual, false, null, 1, DateTimeOffset.UnixEpoch);
            Items.Add(item);
            return ApiResult<ShoppingItemDto>.Success(item);
        });
    }

    public async Task<ApiResult<ShoppingItemChangeResult>> ChangeItemAsync(
        Guid householdId, Guid itemId, ChangeShoppingItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (HoldChange is not null)
        {
            await HoldChange.Task.WaitAsync(HoldTimeout, cancellationToken).ConfigureAwait(false);
        }

        Calls.Add("change-item:" + itemId);
        Changes.Add(request);
        if (FailWith is not null || FailChangeWith is not null)
        {
            var failure = (FailWith ?? FailChangeWith)!;
            if (FailChangeOnce)
            {
                FailChangeWith = null;
            }

            return ApiResult<ShoppingItemChangeResult>.Failure(failure);
        }

        var index = Items.FindIndex(i => i.Id == itemId);
        if (index < 0)
        {
            return ApiResult<ShoppingItemChangeResult>.Failure(NotFound(ShoppingErrorCodes.ItemNotFound));
        }

        var current = Items[index];
        if (ChangeOutcome == ShoppingConflictOutcomeDto.CheckedWins && !request.Checked)
        {
            // The server kept its state: the item that won is the checked one.
            var kept = current with { Checked = true, CheckedAt = DateTimeOffset.UnixEpoch, Version = current.Version + 1 };
            Items[index] = kept;
            return ApiResult<ShoppingItemChangeResult>.Success(new(kept, ChangeOutcome));
        }

        var updated = current with
        {
            Checked = request.Checked, Quantity = request.Quantity, Note = request.Note, Text = request.Text,
            SectionKey = request.SectionKey, CheckedAt = request.Checked ? DateTimeOffset.UnixEpoch : null, Version = current.Version + 1,
        };
        Items[index] = updated;
        if (HoldResponse is not null)
        {
            await HoldResponse.Task.WaitAsync(HoldTimeout, cancellationToken).ConfigureAwait(false);
        }

        return ApiResult<ShoppingItemChangeResult>.Success(new(updated, ChangeOutcome));
    }

    public Task<ApiResult<ShoppingItemChangeResult>> DeleteItemAsync(
        Guid householdId, Guid itemId, long baseVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("delete-item:" + itemId + ":" + baseVersion, () =>
        {
            var current = Items.FirstOrDefault(i => i.Id == itemId);
            if (current is null)
            {
                return ApiResult<ShoppingItemChangeResult>.Failure(NotFound(ShoppingErrorCodes.ItemNotFound));
            }

            if (FailDeleteWith is not null)
            {
                return ApiResult<ShoppingItemChangeResult>.Failure(FailDeleteWith);
            }

            if (DeleteOutcome is ShoppingConflictOutcomeDto.NoConflict or ShoppingConflictOutcomeDto.BothDeleted)
            {
                Items.Remove(current);
            }

            return ApiResult<ShoppingItemChangeResult>.Success(new(current, DeleteOutcome));
        }));

    public Task<ApiResult<ShoppingItemDto>> MoveItemAsync(
        Guid householdId, Guid itemId, MoveShoppingItemRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Respond("move-item:" + itemId + ">" + request?.ListId, () =>
        {
            var index = Items.FindIndex(i => i.Id == itemId);
            if (index < 0)
            {
                return ApiResult<ShoppingItemDto>.Failure(NotFound(ShoppingErrorCodes.ItemNotFound));
            }

            Items[index] = Items[index] with { ListId = request!.ListId, Version = Items[index].Version + 1 };
            return ApiResult<ShoppingItemDto>.Success(Items[index]);
        }));
}

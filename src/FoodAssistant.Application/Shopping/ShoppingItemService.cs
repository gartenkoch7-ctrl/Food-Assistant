using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Reading, adding and moving shopping list entries. Changes and deletions go through the
/// conflict rules in <see cref="ShoppingItemChangeService"/>.
/// </summary>
public sealed class ShoppingItemService(
    IShoppingItemStore items,
    IShoppingListStore lists,
    IHouseholdContext householdContext,
    IActivityLog activityLog,
    TimeProvider time)
{
    public async Task<OperationResult<IReadOnlyList<ShoppingItemDto>>> ListAsync(
        Guid householdId,
        Guid listId,
        bool includeChecked,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        if (!await lists.ListExistsAsync(listId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<IReadOnlyList<ShoppingItemDto>>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        var records = await items.ListItemsAsync(listId, includeChecked, cancellationToken).ConfigureAwait(false);
        return OperationResult<IReadOnlyList<ShoppingItemDto>>.Ok([.. records.Select(ShoppingMapping.ToDto)]);
    }

    /// <summary>
    /// Adds an entry by hand (every role, also restricted). A known id returns the existing entry
    /// (<see cref="OperationStatus.Ok"/>) instead of a second one, so queued requests can be repeated.
    /// </summary>
    public async Task<OperationResult<ShoppingItemDto>> AddAsync(
        Guid householdId,
        Guid listId,
        CreateShoppingItemRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.add_item");

        var result = await AddCoreAsync(listId, request, userId, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    /// <summary>
    /// Adds an entry created by the server itself (minimum stock, recurring rules). Same rules as
    /// <see cref="AddAsync"/>; the caller supplies a valid item and an existing list of the household.
    /// </summary>
    /// <exception cref="ArgumentException">The item breaks the input rules.</exception>
    /// <exception cref="InvalidOperationException">The list does not exist or the id belongs to another household.</exception>
    public async Task<ShoppingItemDto> AddInternalAsync(
        Guid householdId,
        Guid listId,
        NewShoppingItem item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        householdContext.EnsureActive(householdId);
        if (!ShoppingValidation.IsValid(item))
        {
            throw new ArgumentException("The shopping item breaks the input rules.", nameof(item));
        }

        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.add_item");
        if (!await lists.ListExistsAsync(listId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The shopping list does not exist in the active household.");
        }

        if (await items.GetItemAsync(item.Id, includeDeleted: true, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            activity?.SetTag("foodassistant.outcome", nameof(OperationStatus.Ok));
            return ShoppingMapping.ToDto(existing);
        }

        var normalized = item with { Text = item.Text.Trim(), Note = ShoppingValidation.NormalizeNote(item.Note) };
        var (added, created, listGone) = await InsertAsync(listId, normalized, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", created ? nameof(OperationStatus.Created) : nameof(OperationStatus.Ok));
        return added ?? throw new InvalidOperationException(listGone
            ? "The shopping list was deleted meanwhile."
            : "The shopping item id is already used by another household.");
    }

    /// <summary>Moves an entry to another list of the household (member and owner).</summary>
    public async Task<OperationResult<ShoppingItemDto>> MoveAsync(
        Guid householdId,
        Guid itemId,
        MoveShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.move_item");

        var result = await ConcurrencyRetry.RunAsync(
                () => TryMoveAsync(itemId, request.ListId, cancellationToken))
            .ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    private async Task<OperationResult<ShoppingItemDto>> AddCoreAsync(
        Guid listId,
        CreateShoppingItemRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var errors = ShoppingValidation.ValidateNewItem(request);
        if (errors.Count > 0)
        {
            return OperationResult<ShoppingItemDto>.Invalid(errors, ShoppingErrorCodes.ValidationFailed);
        }

        if (!await lists.ListExistsAsync(listId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ShoppingItemDto>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        var id = request.Id ?? Guid.CreateVersion7();
        if (await items.GetItemAsync(id, includeDeleted: true, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return OperationResult<ShoppingItemDto>.Ok(ShoppingMapping.ToDto(existing));
        }

        string text, sectionKey;
        if (request.ProductId is { } productId)
        {
            var product = await items.GetProductForItemAsync(productId, cancellationToken).ConfigureAwait(false);
            if (product is not { } p)
            {
                return OperationResult<ShoppingItemDto>.NotFound(CatalogErrorCodes.ProductNotFound);
            }

            text = p.Name;
            sectionKey = p.SectionKey ?? StoreSections.ForCategory(p.CategoryKey);
        }
        else
        {
            text = request.Text!.Trim();
            sectionKey = request.SectionKey ?? StoreSections.Other;
        }

        var item = new NewShoppingItem(
            id,
            request.ProductId,
            text,
            ShoppingMapping.ToQuantity(request.Quantity),
            ShoppingValidation.NormalizeNote(request.Note),
            sectionKey,
            ShoppingItemOrigin.Manual,
            userId);
        var (added, created, listGone) = await InsertAsync(listId, item, cancellationToken).ConfigureAwait(false);
        return added is not null
            ? (created ? OperationResult<ShoppingItemDto>.Created(added) : OperationResult<ShoppingItemDto>.Ok(added))
            : listGone
                ? OperationResult<ShoppingItemDto>.NotFound(ShoppingErrorCodes.ListNotFound)
                : OperationResult<ShoppingItemDto>.Conflict(ErrorCodes.Conflict);
    }

    // Inserts the entry. If a concurrent request inserted the same id first, returns that entry
    // (not created). Otherwise no entry: the list was deleted meanwhile, or the id belongs to
    // another household.
    private async Task<(ShoppingItemDto? Item, bool Created, bool ListGone)> InsertAsync(
        Guid listId,
        NewShoppingItem item,
        CancellationToken cancellationToken)
    {
        activityLog.Add(new ActivityEntry(
            ActivityArea.Shopping, ShoppingActivityKinds.ItemAdded, item.Id, item.ProductId, item.CreatedByUserId, null, 1));
        var added = await items.AddItemAsync(listId, item, ShoppingTime.Now(time), cancellationToken).ConfigureAwait(false);
        if (added is not null)
        {
            return (ShoppingMapping.ToDto(added), true, false);
        }

        if (await items.GetItemAsync(item.Id, includeDeleted: true, cancellationToken).ConfigureAwait(false) is { } raced)
        {
            return (ShoppingMapping.ToDto(raced), false, false);
        }

        return (null, false, !await lists.ListExistsAsync(listId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<OperationResult<ShoppingItemDto>?> TryMoveAsync(
        Guid itemId,
        Guid targetListId,
        CancellationToken cancellationToken)
    {
        var item = await items.GetItemAsync(itemId, includeDeleted: false, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return OperationResult<ShoppingItemDto>.NotFound(ShoppingErrorCodes.ItemNotFound);
        }

        if (!await lists.ListExistsAsync(targetListId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<ShoppingItemDto>.NotFound(ShoppingErrorCodes.ListNotFound);
        }

        if (item.ListId == targetListId)
        {
            return OperationResult<ShoppingItemDto>.Ok(ShoppingMapping.ToDto(item));
        }

        var moved = item with { ListId = targetListId, Version = item.Version + 1, UpdatedAt = ShoppingTime.Now(time) };
        return await items.ApplyItemAsync(moved, item.Version, cancellationToken).ConfigureAwait(false)
            ? OperationResult<ShoppingItemDto>.Ok(ShoppingMapping.ToDto(moved))
            : null;
    }
}

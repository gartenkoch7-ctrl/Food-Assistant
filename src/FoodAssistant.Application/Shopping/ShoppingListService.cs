using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Shopping lists of the active household. Every household has exactly one default list; it is
/// created without a name the first time lists are read (docs/plan/features/m01-d-einkauf.md).
/// </summary>
public sealed class ShoppingListService(
    IShoppingListStore store,
    IHouseholdContext householdContext,
    TimeProvider time,
    ILogger<ShoppingListService> logger)
{
    /// <summary>All lists; creates the default list first if the household has none.</summary>
    public async Task<IReadOnlyList<ShoppingListDto>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var lists = await store.ListListsAsync(cancellationToken).ConfigureAwait(false);
        if (lists.Any(l => l.IsDefault))
        {
            return lists;
        }

        await store.EnsureDefaultListAsync(time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return await store.ListListsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The default list, created if missing (target of minimum stock and "add without list").</summary>
    public Task<ShoppingListDto> EnsureDefaultListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        return store.EnsureDefaultListAsync(time.GetUtcNow(), cancellationToken);
    }

    public async Task<OperationResult<ShoppingListDto>> CreateAsync(
        Guid householdId,
        CreateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.create_list");

        var errors = await ValidateAsync(request.Name, request.StoreId, cancellationToken).ConfigureAwait(false);
        var result = errors.Count > 0
            ? OperationResult<ShoppingListDto>.Invalid(errors, ShoppingErrorCodes.ValidationFailed)
            : await store.AddListAsync(
                    request.Id ?? Guid.CreateVersion7(),
                    request.Name.Trim(),
                    request.StoreId,
                    time.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Status == OperationStatus.Created)
        {
            ShoppingLog.ShoppingListCreated(logger, result.Value!.Id);
        }

        return result;
    }

    public async Task<OperationResult<ShoppingListDto>> UpdateAsync(
        Guid householdId,
        Guid listId,
        long? expectedVersion,
        UpdateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.update_list");

        var errors = await ValidateAsync(request.Name, request.StoreId, cancellationToken).ConfigureAwait(false);
        var result = errors.Count > 0
            ? OperationResult<ShoppingListDto>.Invalid(errors, ShoppingErrorCodes.ValidationFailed)
            : await store.UpdateListAsync(
                    listId,
                    expectedVersion,
                    request with { Name = request.Name.Trim() },
                    time.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    public async Task<OperationResult<ShoppingListDto>> DeleteAsync(
        Guid householdId,
        Guid listId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.delete_list");

        var result = await store.DeleteListAsync(listId, expectedVersion, cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Status == OperationStatus.Deleted)
        {
            ShoppingLog.ShoppingListDeleted(logger, listId);
        }

        return result;
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(
        string? name,
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(ShoppingValidation.ValidateListName(name), StringComparer.Ordinal);
        if (storeId is { } id && !await store.StoreExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            // A field error, not 404: the list in the path exists, only the referenced store does not.
            errors["storeId"] = ["not_found"];
        }

        return errors;
    }
}

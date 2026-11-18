using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Catalog;

/// <summary>Storage locations of the household (fridge, freezer, pantry …).</summary>
public sealed class StorageLocationService(
    ICatalogStore store,
    IHouseholdContext household,
    TimeProvider time,
    ILogger<StorageLocationService> logger)
{
    public Task<IReadOnlyList<StorageLocationDto>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        return store.ListLocationsAsync(cancellationToken);
    }

    public async Task<OperationResult<StorageLocationDto>> CreateAsync(
        Guid householdId,
        StorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.create_location");

        var errors = Validate(request, includeId: true);
        if (errors.Count > 0)
        {
            return Finish(activity, OperationResult<StorageLocationDto>.Invalid(errors, CatalogErrorCodes.ValidationFailed));
        }

        var location = new NewStorageLocation(
            request.Id ?? Guid.CreateVersion7(),
            householdId,
            request.Name.Trim(),
            ToDomain(request.Type),
            request.SortOrder,
            time.GetUtcNow());

        var result = await store.AddLocationAsync(location, cancellationToken).ConfigureAwait(false);
        if (result.Status == OperationStatus.Created)
        {
            CatalogLog.StorageLocationCreated(logger, location.Id, householdId);
        }

        return Finish(activity, result);
    }

    /// <param name="expectedVersion">From <c>If-Match</c>; <see langword="null"/> means any version.</param>
    public async Task<OperationResult<StorageLocationDto>> UpdateAsync(
        Guid householdId,
        Guid locationId,
        long? expectedVersion,
        StorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.update_location");

        var errors = Validate(request, includeId: false);
        if (errors.Count > 0)
        {
            return Finish(activity, OperationResult<StorageLocationDto>.Invalid(errors, CatalogErrorCodes.ValidationFailed));
        }

        var changes = new StorageLocationChanges(request.Name.Trim(), ToDomain(request.Type), request.SortOrder, time.GetUtcNow());
        return Finish(activity, await store.UpdateLocationAsync(locationId, expectedVersion, changes, cancellationToken).ConfigureAwait(false));
    }

    public async Task<OperationResult<StorageLocationDto>> DeleteAsync(
        Guid householdId,
        Guid locationId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        household.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.catalog.delete_location");

        var result = await store.DeleteLocationAsync(locationId, expectedVersion, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (result.Status == OperationStatus.Deleted)
        {
            CatalogLog.StorageLocationDeleted(logger, locationId, householdId);
        }

        return Finish(activity, result);
    }

    private static Dictionary<string, string[]> Validate(StorageLocationRequest request, bool includeId)
    {
        var errors = new Dictionary<string, string[]>(CatalogValidation.ValidateLocation(request.Name), StringComparer.Ordinal);
        if (!Enum.IsDefined(request.Type))
        {
            errors["type"] = ["unknown"];
        }

        if (includeId && request.Id == Guid.Empty)
        {
            errors["id"] = ["invalid"];
        }

        return errors;
    }

    // The contract enum mirrors the domain enum member by member (ContractEnumTests).
    private static StorageType ToDomain(StorageTypeDto type) => (StorageType)(int)type;

    private static OperationResult<StorageLocationDto> Finish(System.Diagnostics.Activity? activity, OperationResult<StorageLocationDto> result)
    {
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }
}

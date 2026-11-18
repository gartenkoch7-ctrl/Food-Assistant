using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Stores;

/// <summary>Stores of the active household and the section order of each (learned, manual overrides).</summary>
public sealed class StoreService(
    IStoreStore store,
    IStoreSectionOrderStore sectionOrder,
    IHouseholdContext householdContext,
    TimeProvider time,
    ILogger<StoreService> logger)
{
    public const int NameMaxLength = 80;

    private static readonly IReadOnlyDictionary<string, int> NoManualOrder = new Dictionary<string, int>(StringComparer.Ordinal);

    public Task<IReadOnlyList<StoreDto>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        return store.ListAsync(cancellationToken);
    }

    public async Task<OperationResult<StoreDto>> CreateAsync(Guid householdId, StoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.stores.create");

        var errors = ValidateName(request.Name);
        var result = errors.Count > 0
            ? OperationResult<StoreDto>.Invalid(errors, StoreErrorCodes.ValidationFailed)
            : await store.AddAsync(request.Id ?? Guid.CreateVersion7(), request.Name.Trim(), time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Status == OperationStatus.Created)
        {
            StoresLog.StoreCreated(logger, result.Value!.Id);
        }

        return result;
    }

    public async Task<OperationResult<StoreDto>> UpdateAsync(
        Guid householdId,
        Guid storeId,
        long? expectedVersion,
        StoreRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.stores.update");

        var errors = ValidateName(request.Name);
        var result = errors.Count > 0
            ? OperationResult<StoreDto>.Invalid(errors, StoreErrorCodes.ValidationFailed)
            : await store.UpdateAsync(storeId, expectedVersion, request.Name.Trim(), time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    public async Task<OperationResult<StoreDto>> DeleteAsync(
        Guid householdId,
        Guid storeId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.stores.delete");

        var result = await store.DeleteAsync(storeId, expectedVersion, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Status == OperationStatus.Deleted)
        {
            StoresLog.StoreDeleted(logger, storeId);
        }

        return result;
    }

    /// <summary>Section order of a store; without a store the default order.</summary>
    public async Task<OperationResult<SectionOrderDto>> GetSectionOrderAsync(
        Guid householdId,
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        if (storeId is not { } id)
        {
            return OperationResult<SectionOrderDto>.Ok(new SectionOrderDto(null, StoreSections.All, HasManualOrder: false));
        }

        return await store.ExistsAsync(id, cancellationToken).ConfigureAwait(false)
            ? OperationResult<SectionOrderDto>.Ok(await ReadOrderAsync(id, cancellationToken).ConfigureAwait(false))
            : OperationResult<SectionOrderDto>.NotFound(StoreErrorCodes.StoreNotFound);
    }

    /// <summary>Places sections by hand; the remaining ones follow the learned order. An empty list resets.</summary>
    public async Task<OperationResult<SectionOrderDto>> SetSectionOrderAsync(
        Guid householdId,
        Guid storeId,
        SetSectionOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.stores.set_section_order");

        var result = await SetCoreAsync(storeId, request.Sections, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    /// <summary>Drops the manual order; the learned order applies again.</summary>
    public async Task<OperationResult<SectionOrderDto>> ResetSectionOrderAsync(
        Guid householdId,
        Guid storeId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.stores.reset_section_order");

        var result = await SetCoreAsync(storeId, [], cancellationToken).ConfigureAwait(false);
        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    /// <summary>Errors of a manual order: required, only known keys, each at most once.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateSections(IReadOnlyList<string>? sections)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (sections is null)
        {
            errors["sections"] = ["required"];
        }
        else if (!sections.All(StoreSections.IsKnown))
        {
            errors["sections"] = ["unknown"];
        }
        else if (sections.Distinct(StringComparer.Ordinal).Count() != sections.Count)
        {
            errors["sections"] = ["duplicate"];
        }

        return errors;
    }

    /// <summary>Errors of a store name: required, at most 80 characters after trimming.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateName(string? name)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["required"];
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors["name"] = ["too_long"];
        }

        return errors;
    }

    private async Task<OperationResult<SectionOrderDto>> SetCoreAsync(
        Guid storeId,
        IReadOnlyList<string>? sections,
        CancellationToken cancellationToken)
    {
        var errors = ValidateSections(sections);
        if (errors.Count > 0)
        {
            return OperationResult<SectionOrderDto>.Invalid(errors, StoreErrorCodes.ValidationFailed);
        }

        if (!await store.ExistsAsync(storeId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<SectionOrderDto>.NotFound(StoreErrorCodes.StoreNotFound);
        }

        if (sections!.Count == 0)
        {
            await sectionOrder.ClearManualOrderAsync(storeId, cancellationToken).ConfigureAwait(false);

            // Deleted meanwhile: deleting nothing succeeded, but there is no order to return.
            if (!await store.ExistsAsync(storeId, cancellationToken).ConfigureAwait(false))
            {
                return OperationResult<SectionOrderDto>.NotFound(StoreErrorCodes.StoreNotFound);
            }

            StoresLog.SectionOrderReset(logger, storeId);
        }
        else
        {
            if (!await sectionOrder.SetManualOrderAsync(storeId, sections, cancellationToken).ConfigureAwait(false))
            {
                return OperationResult<SectionOrderDto>.NotFound(StoreErrorCodes.StoreNotFound);
            }

            StoresLog.SectionOrderSet(logger, storeId);
        }

        return OperationResult<SectionOrderDto>.Ok(await ReadOrderAsync(storeId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<SectionOrderDto> ReadOrderAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var transitions = await sectionOrder.GetTransitionsAsync(storeId, cancellationToken).ConfigureAwait(false);
        var manual = await sectionOrder.GetManualOrderAsync(storeId, cancellationToken).ConfigureAwait(false);
        return new SectionOrderDto(
            storeId,
            SectionOrderLearner.Order(transitions, manual.Count > 0 ? manual : NoManualOrder),
            HasManualOrder: manual.Count > 0);
    }
}

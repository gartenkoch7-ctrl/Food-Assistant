using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Households;

/// <summary>Reading and changing name, time zone and expiry lead time of the active household.</summary>
public sealed class HouseholdSettingsService(
    IHouseholdSettingsStore store,
    IHouseholdContext householdContext,
    ILogger<HouseholdSettingsService> logger)
{
    public const int MaxExpiryLeadDays = 14;

    public async Task<OperationResult<HouseholdSettingsDto>> GetAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        var settings = await store.GetAsync(householdId, cancellationToken).ConfigureAwait(false);
        return settings is null
            ? OperationResult<HouseholdSettingsDto>.NotFound(HouseholdErrorCodes.NotFound)
            : OperationResult<HouseholdSettingsDto>.Ok(settings);
    }

    public async Task<OperationResult<HouseholdSettingsDto>> UpdateAsync(
        Guid householdId,
        UpdateHouseholdRequest request,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.households.update_settings");

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        HouseholdValidation.Validate(request.Name, request.TimeZone, "name", "timeZone", errors);
        if (request.ExpiryLeadDays is < 0 or > MaxExpiryLeadDays)
        {
            errors["expiryLeadDays"] = ["out_of_range"];
        }

        if (errors.Count > 0)
        {
            return OperationResult<HouseholdSettingsDto>.Invalid(errors, HouseholdErrorCodes.ValidationFailed);
        }

        var result = await store
            .UpdateAsync(householdId, expectedVersion, request.Name.Trim(), request.TimeZone, request.ExpiryLeadDays, cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Succeeded)
        {
            HouseholdsLog.SettingsChanged(logger, householdId);
        }

        return result;
    }
}

using FoodAssistant.Application.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Households;

/// <summary>Creating further households (server admin only, ADR-0002).</summary>
public sealed class HouseholdService(IHouseholdStore store, ILogger<HouseholdService> logger)
{
    /// <summary>
    /// Creates a household; the creating admin becomes its first owner so the household is never
    /// without an owner and can invite its real owners right away.
    /// </summary>
    public async Task<CreateHouseholdResult> CreateAsync(
        string? name,
        string? timeZoneId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("foodassistant.households.create");

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        HouseholdValidation.Validate(name, timeZoneId, "name", "timeZone", errors);
        if (errors.Count > 0)
        {
            return new CreateHouseholdResult(null, errors);
        }

        var household = await store
            .CreateAsync(new NewHousehold(name!.Trim(), timeZoneId!), adminUserId, cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("foodassistant.household_id", household.Id);
        HouseholdsLog.HouseholdCreated(logger, household.Id);
        return new CreateHouseholdResult(household, errors);
    }
}

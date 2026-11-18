using FoodAssistant.Application.Common;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Persistence of the settings of one household (name, time zone, expiry lead time).</summary>
public interface IHouseholdSettingsStore
{
    /// <summary>The settings, or <see langword="null"/> when the household does not exist or was deleted.</summary>
    Task<HouseholdSettingsDto?> GetAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the settings. <paramref name="expectedVersion"/> <see langword="null"/> skips the version
    /// check (<c>If-Match: *</c>); a stale version yields <see cref="OperationStatus.VersionMismatch"/>.
    /// </summary>
    Task<OperationResult<HouseholdSettingsDto>> UpdateAsync(
        Guid householdId,
        long? expectedVersion,
        string name,
        string timeZone,
        int expiryLeadDays,
        CancellationToken cancellationToken);
}

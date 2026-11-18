using FoodAssistant.Application.Common;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Households;

internal sealed class HouseholdSettingsStore(AppDbContext db) : IHouseholdSettingsStore
{
    public async Task<HouseholdSettingsDto?> GetAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await db.Households
            .AsNoTracking()
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken)
            .ConfigureAwait(false);
        return household is null ? null : ToDto(household);
    }

    public async Task<OperationResult<HouseholdSettingsDto>> UpdateAsync(
        Guid householdId,
        long? expectedVersion,
        string name,
        string timeZone,
        int expiryLeadDays,
        CancellationToken cancellationToken)
    {
        var household = await db.Households
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken)
            .ConfigureAwait(false);
        if (household is null)
        {
            return OperationResult<HouseholdSettingsDto>.NotFound(HouseholdErrorCodes.NotFound);
        }

        if (expectedVersion is { } expected)
        {
            if (expected != household.Version)
            {
                return Mismatch(household);
            }

            // A concurrent save between loading and saving shows up as DbUpdateConcurrencyException.
            db.Entry(household).Property(h => h.Version).OriginalValue = (uint)expected;
        }

        household.Name = name;
        household.TimeZoneId = timeZone;
        household.ExpiryLeadDays = expiryLeadDays;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var current = await db.Households.AsNoTracking()
                .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken)
                .ConfigureAwait(false);
            return current is null
                ? OperationResult<HouseholdSettingsDto>.NotFound(HouseholdErrorCodes.NotFound)
                : Mismatch(current);
        }

        // xmin changed with the save; the tracked entity carries the new value after SaveChanges.
        return OperationResult<HouseholdSettingsDto>.Ok(ToDto(household));
    }

    private static OperationResult<HouseholdSettingsDto> Mismatch(Household household) =>
        OperationResult<HouseholdSettingsDto>.VersionMismatch(household.Version, ToDto(household));

    private static HouseholdSettingsDto ToDto(Household household) =>
        new(household.Id, household.Name, household.TimeZoneId, household.ExpiryLeadDays, household.Version);
}

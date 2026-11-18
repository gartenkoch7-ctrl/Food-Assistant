using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Households;

/// <summary>Listing members, changing roles and removing members of the active household.</summary>
public sealed class MemberService(
    IHouseholdStore store,
    IHouseholdContext householdContext,
    ILogger<MemberService> logger)
{
    public Task<IReadOnlyList<HouseholdMemberResponse>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        return store.GetMembersAsync(householdId, cancellationToken);
    }

    public async Task<MembershipChangeOutcome> ChangeRoleAsync(
        Guid householdId,
        Guid userId,
        HouseholdRole newRole,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        if (!HouseholdRoles.IsDefined(newRole))
        {
            throw new ArgumentOutOfRangeException(nameof(newRole), "Unknown household role.");
        }

        using var activity = Telemetry.Source.StartActivity("foodassistant.households.change_member_role");

        var outcome = await store.ChangeRoleAsync(householdId, userId, newRole, cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", outcome.ToString());
        if (outcome == MembershipChangeOutcome.Succeeded)
        {
            HouseholdsLog.MemberRoleChanged(logger, householdId, userId, newRole);
        }
        else if (outcome == MembershipChangeOutcome.LastOwner)
        {
            HouseholdsLog.LastOwnerProtected(logger, householdId);
        }

        return outcome;
    }

    public async Task<MembershipChangeOutcome> RemoveAsync(Guid householdId, Guid userId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.households.remove_member");

        var outcome = await store.RemoveMemberAsync(householdId, userId, cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", outcome.ToString());
        if (outcome == MembershipChangeOutcome.Succeeded)
        {
            HouseholdsLog.MemberRemoved(logger, householdId, userId);
        }
        else if (outcome == MembershipChangeOutcome.LastOwner)
        {
            HouseholdsLog.LastOwnerProtected(logger, householdId);
        }

        return outcome;
    }
}

using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Households;

/// <summary>Persistence of households and memberships. Deleted households are invisible everywhere.</summary>
public interface IHouseholdStore
{
    /// <summary>The membership of a person in a (not deleted) household, or <see langword="null"/>.</summary>
    Task<HouseholdMembershipInfo?> FindMembershipAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Ids of all not deleted households, for background services (<see cref="IHouseholdScopeFactory"/>).</summary>
    Task<IReadOnlyList<Guid>> ListActiveHouseholdIdsAsync(CancellationToken cancellationToken);

    /// <summary>All households of a person with their role, ordered by name.</summary>
    Task<IReadOnlyList<MeHouseholdResponse>> GetHouseholdsOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Creates a household and makes <paramref name="ownerUserId"/> its first owner.</summary>
    Task<HouseholdResponse> CreateAsync(NewHousehold household, Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Members of a household, ordered by display name.</summary>
    Task<IReadOnlyList<HouseholdMemberResponse>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Changes a member's role; refuses to remove the last owner (checked under a household lock).</summary>
    Task<MembershipChangeOutcome> ChangeRoleAsync(
        Guid householdId,
        Guid userId,
        HouseholdRole newRole,
        CancellationToken cancellationToken);

    /// <summary>Removes a member; refuses to remove the last owner (checked under a household lock).</summary>
    Task<MembershipChangeOutcome> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);
}

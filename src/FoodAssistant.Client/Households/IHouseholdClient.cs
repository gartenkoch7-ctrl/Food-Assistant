using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Client.Households;

/// <summary>Settings, members and invitations of one household (ADR-0002). Changing settings needs the ETag of the read.</summary>
public interface IHouseholdClient
{
    Task<ApiResult<HouseholdSettingsDto>> GetSettingsAsync(Guid householdId, CancellationToken cancellationToken);

    /// <param name="eTag">ETag of the settings the change is based on (<see cref="ApiResult{T}.ETag"/> of the read).</param>
    Task<ApiResult<HouseholdSettingsDto>> UpdateSettingsAsync(
        Guid householdId, UpdateHouseholdRequest request, string eTag, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<HouseholdMemberResponse>>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken);

    Task<ApiResult> ChangeMemberRoleAsync(Guid householdId, Guid userId, HouseholdRole role, CancellationToken cancellationToken);

    Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Creates an invitation; the token is in the answer only this once.</summary>
    Task<ApiResult<CreatedInvitationResponse>> CreateInvitationAsync(Guid householdId, HouseholdRole role, CancellationToken cancellationToken);

    Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken);
}

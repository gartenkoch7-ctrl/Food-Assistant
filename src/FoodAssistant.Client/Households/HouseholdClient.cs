using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Client.Households;

public sealed class HouseholdClient(ApiHttpClient api) : IHouseholdClient
{
    // ApiPaths.Household adds a trailing slash for an empty path; the settings live on the household itself.
    private static string Own(Guid householdId) => $"api/v1/households/{householdId:D}";

    public Task<ApiResult<HouseholdSettingsDto>> GetSettingsAsync(Guid householdId, CancellationToken cancellationToken) =>
        api.GetAsync<HouseholdSettingsDto>(Own(householdId), cancellationToken);

    public Task<ApiResult<HouseholdSettingsDto>> UpdateSettingsAsync(
        Guid householdId, UpdateHouseholdRequest request, string eTag, CancellationToken cancellationToken) =>
        api.SendAsync<UpdateHouseholdRequest, HouseholdSettingsDto>(HttpMethod.Put, Own(householdId), request, eTag, null, cancellationToken);

    public async Task<ApiResult<IReadOnlyList<HouseholdMemberResponse>>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken) =>
        ToReadOnly(await api.GetAsync<List<HouseholdMemberResponse>>(ApiPaths.Household(householdId, "members"), cancellationToken).ConfigureAwait(false));

    public Task<ApiResult> ChangeMemberRoleAsync(Guid householdId, Guid userId, HouseholdRole role, CancellationToken cancellationToken) =>
        api.SendAsync(HttpMethod.Put, ApiPaths.Household(householdId, $"members/{userId:D}/role"), new ChangeMemberRoleRequest(role), null, null, cancellationToken);

    public Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken) =>
        api.SendAsync<object>(HttpMethod.Delete, ApiPaths.Household(householdId, $"members/{userId:D}"), null, null, null, cancellationToken);

    public async Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken) =>
        ToReadOnly(await api.GetAsync<List<InvitationResponse>>(ApiPaths.Household(householdId, "invitations"), cancellationToken).ConfigureAwait(false));

    public Task<ApiResult<CreatedInvitationResponse>> CreateInvitationAsync(Guid householdId, HouseholdRole role, CancellationToken cancellationToken) =>
        api.SendAsync<CreateInvitationRequest, CreatedInvitationResponse>(
            HttpMethod.Post, ApiPaths.Household(householdId, "invitations"), new CreateInvitationRequest(role), null, null, cancellationToken);

    public Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken) =>
        api.SendAsync<object>(HttpMethod.Delete, ApiPaths.Household(householdId, $"invitations/{invitationId:D}"), null, null, null, cancellationToken);

    private static ApiResult<IReadOnlyList<T>> ToReadOnly<T>(ApiResult<List<T>> result) =>
        result.IsSuccess
            ? ApiResult<IReadOnlyList<T>>.Success(result.Value!, result.ETag)
            : ApiResult<IReadOnlyList<T>>.Failure(result.Error!);
}

using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Households;

/// <summary>
/// <c>/api/v1/households/{householdId}/members</c>: members and owners may list, only owners
/// change roles or remove members; the last owner is protected (ADR-0002).
/// </summary>
internal static class MemberEndpoints
{
    public static RouteGroupBuilder MapMemberEndpoints(this RouteGroupBuilder household)
    {
        var members = household.MapGroup("/members").WithTags("Members");

        members.MapGet("", ListAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        members.MapPut("/{userId:guid}/role", ChangeRoleAsync).RequireAuthorization(AuthPolicies.HouseholdOwner);
        members.MapDelete("/{userId:guid}", RemoveAsync).RequireAuthorization(AuthPolicies.HouseholdOwner);
        return household;
    }

    private static async Task<IResult> ListAsync(
        Guid householdId,
        MemberService members,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await members.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> ChangeRoleAsync(
        Guid householdId,
        Guid userId,
        ChangeMemberRoleRequest request,
        MemberService members,
        CancellationToken cancellationToken)
    {
        if (!HouseholdRoles.IsDefined(request.Role))
        {
            return InvitationEndpoints.InvalidRole();
        }

        var outcome = await members.ChangeRoleAsync(householdId, userId, request.Role, cancellationToken).ConfigureAwait(false);
        return ToResult(outcome);
    }

    private static async Task<IResult> RemoveAsync(
        Guid householdId,
        Guid userId,
        MemberService members,
        CancellationToken cancellationToken) =>
        ToResult(await members.RemoveAsync(householdId, userId, cancellationToken).ConfigureAwait(false));

    private static IResult ToResult(MembershipChangeOutcome outcome) => outcome switch
    {
        MembershipChangeOutcome.Succeeded => TypedResults.NoContent(),
        MembershipChangeOutcome.LastOwner => ProblemResults.Problem(
            StatusCodes.Status409Conflict,
            HouseholdErrorCodes.LastOwner,
            "The household needs at least one owner."),
        _ => ProblemResults.Problem(
            StatusCodes.Status404NotFound, HouseholdErrorCodes.MemberNotFound, "The member was not found."),
    };
}

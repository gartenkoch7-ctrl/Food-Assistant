using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Households;

/// <summary><c>/api/v1/households/{householdId}/invitations</c> – owners only (ADR-0002).</summary>
internal static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder household)
    {
        var invitations = household.MapGroup("/invitations")
            .WithTags("Invitations")
            .RequireAuthorization(AuthPolicies.HouseholdOwner);

        invitations.MapPost("", CreateAsync);
        invitations.MapGet("", ListAsync);
        invitations.MapDelete("/{invitationId:guid}", RevokeAsync);
        return household;
    }

    private static async Task<IResult> CreateAsync(
        Guid householdId,
        CreateInvitationRequest request,
        HttpContext httpContext,
        InvitationService invitations,
        CancellationToken cancellationToken)
    {
        if (!HouseholdRoles.IsDefined(request.Role))
        {
            return InvalidRole();
        }

        var created = await invitations
            .CreateAsync(householdId, request.Role, httpContext.User.GetRequiredUserId(), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Created((string?)null, created);
    }

    private static async Task<IResult> ListAsync(
        Guid householdId,
        InvitationService invitations,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invitations.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> RevokeAsync(
        Guid householdId,
        Guid invitationId,
        InvitationService invitations,
        CancellationToken cancellationToken)
    {
        var outcome = await invitations.RevokeAsync(householdId, invitationId, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            RevokeInvitationOutcome.Revoked => TypedResults.NoContent(),
            RevokeInvitationOutcome.AlreadyAccepted => ProblemResults.Problem(
                StatusCodes.Status409Conflict,
                InvitationErrorCodes.AlreadyAccepted,
                "The invitation was already accepted."),
            _ => ProblemResults.Problem(
                StatusCodes.Status404NotFound, InvitationErrorCodes.NotFound, "The invitation was not found."),
        };
    }

    internal static IResult InvalidRole() =>
        ProblemResults.Validation(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["role"] = ["invalid_role"] },
            HouseholdErrorCodes.ValidationFailed);
}

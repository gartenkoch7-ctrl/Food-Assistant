using FoodAssistant.Application.Accounts;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// <c>POST /api/v1/invitations/accept</c>: anonymous callers create an account, signed-in callers
/// join a further household. Needs a strict rate limit (ADR-0003) – added with the API track's
/// rate-limit policies.
/// </summary>
internal static class InvitationAcceptEndpoints
{
    public static RouteGroupBuilder MapInvitationAcceptEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/invitations/accept", AcceptAsync)
            .WithTags("Invitations")
            .AllowAnonymous()
            .AddEndpointFilter(new CookieAntiforgeryFilter(always: false));
        return api;
    }

    private static async Task<IResult> AcceptAsync(
        AcceptInvitationRequest request,
        HttpContext httpContext,
        AcceptInvitationService invitations,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = httpContext.User.TryGetUserId(out var userId) ? userId : null;
        var result = await invitations.AcceptAsync(request, currentUserId, cancellationToken).ConfigureAwait(false);
        return result.Status == ProvisioningStatus.Succeeded
            ? TypedResults.Created("/api/v1/me", new AcceptInvitationResponse(result.UserId, result.HouseholdId, result.Role))
            : ProvisioningProblems.ToProblem(result);
    }
}

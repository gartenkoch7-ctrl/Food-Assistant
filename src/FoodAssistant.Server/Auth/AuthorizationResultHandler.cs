using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Turns failed policies into ProblemDetails. A household the caller does not belong to answers
/// <c>404</c> exactly like an unknown one, so existence is never revealed (ADR-0002, ADR-0004).
/// </summary>
internal sealed class AuthorizationResultHandler(ILoggerFactory loggerFactory) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    private readonly ILogger _logger = loggerFactory.CreateLogger(AuthLog.Category);

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Forbidden)
        {
            var problem = ProblemFor(context, policy);
            if (problem is not null)
            {
                await problem.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        await _default.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
    }

    private IResult? ProblemFor(HttpContext context, AuthorizationPolicy policy)
    {
        if (policy.Requirements.OfType<HouseholdRoleRequirement>().Any())
        {
            var membership = context.RequestServices.GetRequiredService<HttpHouseholdContext>().Membership;
            if (membership is null)
            {
                AuthLog.HouseholdNotAccessible(_logger);
                return ProblemResults.Problem(
                    StatusCodes.Status404NotFound, HouseholdErrorCodes.NotFound, "The household was not found.");
            }

            AuthLog.HouseholdRoleInsufficient(_logger, membership.HouseholdId, membership.Role);
            return ProblemResults.Problem(
                StatusCodes.Status403Forbidden,
                HouseholdErrorCodes.Forbidden,
                "Your role in this household does not allow this action.");
        }

        if (policy.Requirements.OfType<ServerAdminRequirement>().Any())
        {
            _ = context.User.TryGetUserId(out var userId);
            AuthLog.ServerAdminDenied(_logger, userId);
            return ProblemResults.Problem(
                StatusCodes.Status403Forbidden, AccountErrorCodes.ServerAdminRequired, "Only the server admin may do this.");
        }

        return null;
    }
}

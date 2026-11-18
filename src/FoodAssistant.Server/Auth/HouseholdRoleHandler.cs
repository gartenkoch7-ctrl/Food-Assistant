using FoodAssistant.Application.Households;
using FoodAssistant.Server.Households;
using Microsoft.AspNetCore.Authorization;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Resolves the caller's membership in the route's household once per request, stores it in
/// <see cref="HttpHouseholdContext"/> (which activates the household query filter) and checks the role.
/// </summary>
internal sealed class HouseholdRoleHandler(IHouseholdStore households, HttpHouseholdContext householdContext)
    : AuthorizationHandler<HouseholdRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HouseholdRoleRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext || !context.User.TryGetUserId(out var userId))
        {
            return;
        }

        if (!householdContext.IsResolved)
        {
            var householdId = ReadHouseholdId(httpContext);
            var membership = householdId is null
                ? null
                : await households.FindMembershipAsync(householdId.Value, userId, httpContext.RequestAborted).ConfigureAwait(false);
            householdContext.SetMembership(membership);
        }

        if (householdContext.Membership is { } current && HouseholdRoles.Satisfies(current.Role, requirement.Minimum))
        {
            context.Succeed(requirement);
        }
    }

    private static Guid? ReadHouseholdId(HttpContext httpContext) =>
        httpContext.GetRouteValue(AuthPolicies.HouseholdRouteValue) switch
        {
            Guid id => id,
            string raw when Guid.TryParse(raw, out var id) => id,
            _ => null,
        };
}

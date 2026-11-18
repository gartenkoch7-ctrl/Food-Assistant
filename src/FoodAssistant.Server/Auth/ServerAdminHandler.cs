using FoodAssistant.Application.Accounts;
using Microsoft.AspNetCore.Authorization;

namespace FoodAssistant.Server.Auth;

/// <summary>
/// Checks the admin flag in the database on every request instead of trusting a claim, so a
/// revoked admin right takes effect immediately, also for still-valid cookies and tokens.
/// </summary>
internal sealed class ServerAdminHandler(IUserAccountStore accounts) : AuthorizationHandler<ServerAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServerAdminRequirement requirement)
    {
        if (!context.User.TryGetUserId(out var userId))
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var account = await accounts.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (account is { IsServerAdmin: true })
        {
            context.Succeed(requirement);
        }
    }
}

using FoodAssistant.Application.Accounts;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Server.Api.Idempotency;
using FoodAssistant.Server.Api.RateLimiting;

namespace FoodAssistant.Server.Auth;

/// <summary><c>/api/v1/setup</c>: first-start wizard, open only while no account exists (ADR-0002).</summary>
internal static class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this RouteGroupBuilder api)
    {
        var setup = api.MapGroup("/setup").WithTags("Setup").AllowAnonymous();
        setup.MapGet("", GetStatusAsync);
        setup.MapPost("", CompleteAsync).RequireRateLimiting(RateLimitPolicies.AuthStrict).WithoutIdempotency();
        return api;
    }

    private static async Task<IResult> GetStatusAsync(SetupService setup, CancellationToken cancellationToken)
    {
        var complete = await setup.IsCompleteAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new SetupStatusResponse(complete));
    }

    private static async Task<IResult> CompleteAsync(
        SetupRequest request,
        SetupService setup,
        CancellationToken cancellationToken)
    {
        var result = await setup.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Status == ProvisioningStatus.Succeeded
            ? TypedResults.Created((string?)null, new SetupResponse(result.UserId, result.HouseholdId))
            : ProvisioningProblems.ToProblem(result);
    }
}

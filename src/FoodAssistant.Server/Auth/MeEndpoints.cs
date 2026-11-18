using FoodAssistant.Application.Accounts;

namespace FoodAssistant.Server.Auth;

/// <summary><c>GET /api/v1/me</c>: the signed-in person and their households with role.</summary>
internal static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", GetAsync).WithTags("Auth").RequireAuthorization();
        return api;
    }

    private static async Task<IResult> GetAsync(HttpContext httpContext, MeService me, CancellationToken cancellationToken)
    {
        var response = await me.GetAsync(httpContext.User.GetRequiredUserId(), cancellationToken).ConfigureAwait(false);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }
}

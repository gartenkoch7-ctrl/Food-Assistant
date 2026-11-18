using FoodAssistant.Contracts.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Api.Info;

/// <summary><c>GET /api/v1/info</c>: version check without sign-in (ADR-0004, ADR-0007).</summary>
internal static class InfoEndpoints
{
    private static readonly string[] ApiVersions = ["v1"];

    public static RouteGroupBuilder MapInfoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/info", GetInfo)
            .WithName("GetServerInfo")
            .WithTags("Info")
            .WithSummary("Server version, supported API and app versions, enabled functions.")
            .AllowAnonymous();
        return group;
    }

    private static Ok<ServerInfoDto> GetInfo(IOptions<ServerInfoOptions> options) =>
        TypedResults.Ok(new ServerInfoDto(
            ServerVersion.Current,
            options.Value.MinimumAppVersion,
            ApiVersions,
            // AI arrives with the AI milestone; until then it is always off.
            new ServerFeaturesDto(Ai: false)));
}

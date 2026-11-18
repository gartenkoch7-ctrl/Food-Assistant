namespace FoodAssistant.Contracts.Common;

/// <summary>
/// Answer of <c>GET /api/v1/info</c> (ADR-0004, ADR-0007). Apps read it before signing in to
/// tell the user when the app or the server is too old.
/// </summary>
/// <param name="ServerVersion">SemVer of the server, may carry build metadata (<c>1.2.0+abc123</c>).</param>
/// <param name="MinimumAppVersion">Oldest app version the server still supports.</param>
/// <param name="ApiVersions">Supported API versions, e.g. <c>v1</c>.</param>
/// <param name="Features">Optional functions that are switched on.</param>
public sealed record ServerInfoDto(
    string ServerVersion,
    string MinimumAppVersion,
    IReadOnlyList<string> ApiVersions,
    ServerFeaturesDto Features);

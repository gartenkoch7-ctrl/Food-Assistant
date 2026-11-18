namespace FoodAssistant.Server.Api.Hosting;

/// <summary>
/// How the server is reached (ADR-0007), section <c>FoodAssistant</c>; set per environment
/// variable, e.g. <c>FOODASSISTANT__PUBLICURL</c>.
/// </summary>
public sealed class HostingOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "FoodAssistant";

    /// <summary>Environment name of automated tests; like Development it does not need a public URL.</summary>
    public const string TestingEnvironment = "Testing";

    /// <summary>
    /// Address under which clients reach the server, e.g. <c>https://food.example.com</c>. Issuer,
    /// redirect URIs and QR codes are built from it. Required outside Development and Testing.
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>
    /// Reverse proxies whose <c>X-Forwarded-For</c>/<c>-Proto</c> headers are trusted: IP addresses
    /// or CIDR networks, separated by commas, e.g. <c>172.16.0.0/12</c> for Docker networks.
    /// Loopback is always trusted. Unset: only loopback, so a proxy on the same host works.
    /// </summary>
    public string? TrustedProxies { get; set; }

    /// <summary>The parsed <see cref="PublicUrl"/>, or <c>null</c> when unset.</summary>
    public Uri? PublicUri =>
        Uri.TryCreate(PublicUrl, UriKind.Absolute, out var uri) ? uri : null;
}

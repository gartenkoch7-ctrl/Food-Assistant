namespace FoodAssistant.Server.Api.RateLimiting;

/// <summary>Log messages of rate limiting (EventIds 1020–1029, catalog: docs/kb/telemetrie.md).</summary>
internal static partial class RateLimitingLog
{
    // Debug: under attack this fires once per rejected request; the metric
    // foodassistant.api.rate_limit.rejections shows the volume. No IP address or person id.
    [LoggerMessage(EventId = 1020, Level = LogLevel.Debug,
        Message = "Rate limit policy {Policy} rejected {HttpMethod} {Route}")]
    public static partial void Rejected(this ILogger logger, string policy, string httpMethod, string route);
}

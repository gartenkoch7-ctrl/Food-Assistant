using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;

namespace FoodAssistant.Server.Api;

/// <summary>Metrics of the API conventions (catalog: docs/kb/telemetrie.md).</summary>
internal static class ApiMetrics
{
    /// <summary>Requests with an <c>Idempotency-Key</c>, tagged <c>outcome</c>.</summary>
    public static readonly Counter<long> IdempotencyRequests = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.api.idempotency.requests",
        unit: "{request}",
        description: "Write requests with an Idempotency-Key by outcome.");

    /// <summary>Requests rejected by a rate limit policy, tagged <c>policy</c>.</summary>
    public static readonly Counter<long> RateLimitRejections = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.api.rate_limit.rejections",
        unit: "{request}",
        description: "Requests rejected with 429 by rate limit policy.");

    public static void IdempotencyOutcome(string outcome) =>
        IdempotencyRequests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public static void RateLimitRejected(string policy) =>
        RateLimitRejections.Add(1, new KeyValuePair<string, object?>("policy", policy));
}

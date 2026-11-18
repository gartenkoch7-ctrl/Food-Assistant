namespace FoodAssistant.Server.Api.RateLimiting;

/// <summary>
/// Named rate limit policies (ADR-0003). Use with <c>.RequireRateLimiting(RateLimitPolicies.AuthStrict)</c>.
/// Limits are configured in <see cref="ApiRateLimitOptions"/>.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Sign-in, password reset, accepting invitations: few requests per minute and IP.</summary>
    public const string AuthStrict = "auth-strict";

    /// <summary>Rest of the API, generous, per person (signed in) or per IP. Applied to the whole <c>/api/v1</c> group.</summary>
    public const string Api = "api";
}

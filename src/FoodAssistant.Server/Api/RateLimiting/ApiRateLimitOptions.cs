namespace FoodAssistant.Server.Api.RateLimiting;

/// <summary>Limits of the <see cref="RateLimitPolicies"/>, section <c>FoodAssistant:RateLimits</c>.</summary>
public sealed class ApiRateLimitOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "FoodAssistant:RateLimits";

    /// <summary>Requests per window and IP for <see cref="RateLimitPolicies.AuthStrict"/>.</summary>
    public int AuthStrictPermitLimit { get; set; } = 5;

    /// <summary>Window length in seconds for <see cref="RateLimitPolicies.AuthStrict"/>.</summary>
    public int AuthStrictWindowSeconds { get; set; } = 60;

    /// <summary>Requests per window and person/IP for <see cref="RateLimitPolicies.Api"/>; covers sync bursts of a household.</summary>
    public int ApiPermitLimit { get; set; } = 600;

    /// <summary>Window length in seconds for <see cref="RateLimitPolicies.Api"/>.</summary>
    public int ApiWindowSeconds { get; set; } = 60;
}

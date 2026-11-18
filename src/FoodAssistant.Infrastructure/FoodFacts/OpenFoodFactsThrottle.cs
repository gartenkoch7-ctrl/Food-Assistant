using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>
/// Our own limit on calls to Open Food Facts (they allow 100 product reads per minute and IP).
/// One instance per server; rejected calls are not queued, the lookup reports "unavailable".
/// </summary>
internal sealed class OpenFoodFactsThrottle : IDisposable
{
    private readonly TokenBucketRateLimiter _limiter;

    public OpenFoodFactsThrottle(IOptions<OpenFoodFactsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var perMinute = options.Value.RequestsPerMinute;
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = perMinute,
            TokensPerPeriod = perMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    public bool TryAcquire()
    {
        using var lease = _limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}

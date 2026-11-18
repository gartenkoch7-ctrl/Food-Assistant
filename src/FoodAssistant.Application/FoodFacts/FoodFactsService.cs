using System.Diagnostics.Metrics;
using FoodAssistant.Application.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.FoodFacts;

/// <summary>
/// Barcode lookup with the server-wide cache: fresh entries are served without a call, expired ones
/// are refreshed and kept as fallback when Open Food Facts cannot be reached.
/// </summary>
public sealed class FoodFactsService(
    IFoodFactsApi api,
    IFoodFactsCache cache,
    TimeProvider time,
    ILogger<FoodFactsService> logger)
{
    private static readonly Counter<long> Lookups = Telemetry.Meter.CreateCounter<long>(
        "foodassistant.food_facts.lookups", "{lookup}", "Barcode lookups by cache state and outcome");

    public async Task<FoodFactsLookup> LookupAsync(string barcode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);
        using var activity = Telemetry.Source.StartActivity("foodassistant.food_facts.lookup");
        var now = time.GetUtcNow();

        var hit = await cache.GetAsync(barcode, now, cancellationToken).ConfigureAwait(false);
        if (hit is not null && hit.ExpiresAt > now)
        {
            return Done(activity, "hit", hit.Lookup with { Stale = false });
        }

        var fetched = await api.FetchAsync(barcode, cancellationToken).ConfigureAwait(false);
        if (fetched.Status == FoodFactsStatus.Unavailable)
        {
            if (hit is not null)
            {
                FoodFactsLog.ServedStale(logger);
                return Done(activity, "stale", hit.Lookup with { Stale = true });
            }

            FoodFactsLog.LookupUnavailable(logger);
            return Done(activity, "miss", FoodFactsLookup.Unavailable);
        }

        var lifetime = fetched.Status == FoodFactsStatus.Found
            ? FoodFactsCachePolicy.FoundLifetime
            : FoodFactsCachePolicy.NotFoundLifetime;
        await cache.StoreAsync(barcode, fetched, now, now + lifetime, cancellationToken).ConfigureAwait(false);
        return Done(activity, "miss", fetched with { Stale = false });
    }

    private static FoodFactsLookup Done(System.Diagnostics.Activity? activity, string cacheState, FoodFactsLookup lookup)
    {
        var outcome = lookup.Status switch
        {
            FoodFactsStatus.Found => "found",
            FoodFactsStatus.NotFound => "not_found",
            _ => "unavailable",
        };
        activity?.SetTag("foodassistant.cache", cacheState);
        activity?.SetTag("foodassistant.outcome", outcome);
        Lookups.Add(1, new KeyValuePair<string, object?>("cache", cacheState), new KeyValuePair<string, object?>("outcome", outcome));
        return lookup;
    }
}

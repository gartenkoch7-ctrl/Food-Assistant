using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.FoodFacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>
/// Open Food Facts product API (v2). Only the barcode is sent. Every failure (disabled, throttled,
/// timeout, network, HTTP error, malformed body) becomes <see cref="FoodFactsStatus.Unavailable"/>
/// so callers can fall back to the cache.
/// </summary>
internal sealed class OpenFoodFactsApi(
    HttpClient http,
    IOptions<OpenFoodFactsOptions> options,
    OpenFoodFactsThrottle throttle,
    ILogger<OpenFoodFactsApi> logger) : IFoodFactsApi
{
    private static readonly Histogram<double> FetchDuration = Telemetry.Meter.CreateHistogram<double>(
        "foodassistant.food_facts.fetch.duration", "s", "Duration of calls to Open Food Facts by outcome");

    // Same assembly version as the server; OFF asks for app name and version in the User-Agent.
    private static readonly string Version =
        typeof(OpenFoodFactsApi).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public async Task<FoodFactsLookup> FetchAsync(string barcode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return FoodFactsLookup.Unavailable;
        }

        if (!throttle.TryAcquire())
        {
            return Fail("throttled");
        }

        using var activity = Telemetry.Source.StartActivity("foodassistant.food_facts.fetch", ActivityKind.Client);
        var started = Stopwatch.GetTimestamp();
        var (lookup, reason, statusCode) = await SendAsync(barcode, settings, cancellationToken).ConfigureAwait(false);

        var outcome = reason ?? (lookup.Status == FoodFactsStatus.Found ? "found" : "not_found");
        activity?.SetTag("foodassistant.outcome", outcome);
        if (statusCode is { } code)
        {
            activity?.SetTag("http.response.status_code", code);
        }

        FetchDuration.Record(
            Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
        return reason is null ? lookup : Fail(reason);
    }

    private async Task<(FoodFactsLookup Lookup, string? FailureReason, int? StatusCode)> SendAsync(
        string barcode,
        OpenFoodFactsOptions settings,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri(barcode, settings));
            request.Headers.TryAddWithoutValidation("User-Agent", $"FoodAssistant/{Version} ({settings.Contact})");
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (FoodFactsLookup.NotFound, null, status);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (FoodFactsLookup.Unavailable, $"http_{status}", status);
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
            return (OpenFoodFactsParser.Parse(document, barcode), null, status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (FoodFactsLookup.Unavailable, "timeout", null);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return (FoodFactsLookup.Unavailable, "network", null);
        }
        catch (JsonException)
        {
            return (FoodFactsLookup.Unavailable, "invalid_response", null);
        }
    }

    private static Uri RequestUri(string barcode, OpenFoodFactsOptions settings) =>
        new(new Uri(settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/"), $"api/v2/product/{Uri.EscapeDataString(barcode)}?fields={OpenFoodFactsParser.Fields}");

    private FoodFactsLookup Fail(string reason)
    {
        OpenFoodFactsLog.FetchFailed(logger, reason);
        return FoodFactsLookup.Unavailable;
    }
}

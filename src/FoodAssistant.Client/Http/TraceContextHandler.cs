using System.Diagnostics;

namespace FoodAssistant.Client.Http;

/// <summary>Passes the current trace on to the server with a W3C <c>traceparent</c> header (AGENTS.md 2.5).</summary>
public sealed class TraceContextHandler : DelegatingHandler
{
    private const string Header = "traceparent";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Headers.Contains(Header))
        {
            var current = Activity.Current;
            var value = current is { IdFormat: ActivityIdFormat.W3C, Id: not null }
                ? current.Id
                : $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01";
            request.Headers.TryAddWithoutValidation(Header, value);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Client.Http;

/// <summary>
/// Cookie-authenticated writes need the antiforgery token (ADR-0003). The token is bound to the
/// sign-in state, so it is fetched lazily and once more when the server says it is stale (e.g. after login).
/// </summary>
public sealed class AntiforgeryHandler : DelegatingHandler
{
    private static readonly string[] WriteMethods = ["POST", "PUT", "PATCH", "DELETE"];
    private static readonly Uri TokenPath = new("api/v1/auth/antiforgery", UriKind.Relative);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private AntiforgeryTokenResponse? _token;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WriteMethods.Contains(request.Method.Method, StringComparer.Ordinal))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Buffer first: a retry has to send the body a second time.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentHeaders = request.Content?.Headers.ToList();

        using var retryTemplate = CloneRequest(request, body, contentHeaders);
        var response = await SendWithTokenAsync(request, refresh: false, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.BadRequest || !await IsStaleTokenAsync(response, cancellationToken).ConfigureAwait(false))
        {
            return response;
        }

        response.Dispose();
        return await SendWithTokenAsync(CloneRequest(retryTemplate, body, contentHeaders), refresh: true, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
        }

        base.Dispose(disposing);
    }

    private static async Task<bool> IsStaleTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Buffering keeps the original answer readable for the caller if this was another 400.
        await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return text.Contains(AccountErrorCodes.AntiforgeryInvalid, StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(HttpRequestMessage request, bool refresh, CancellationToken cancellationToken)
    {
        var (token, failure) = await GetTokenAsync(refresh, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return failure!;
        }

        request.Headers.Remove(token.HeaderName);
        request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <returns>The token, or the server's failed answer (e.g. <c>accounts.https_required</c>) as the caller's response.</returns>
    private async Task<(AntiforgeryTokenResponse? Token, HttpResponseMessage? Failure)> GetTokenAsync(bool refresh, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is null || refresh)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, TokenPath);
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    // Passed on instead of thrown: the caller reads its ProblemDetails like any other answer.
                    return (null, response);
                }

                using (response)
                {
                    _token = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Empty antiforgery response.");
                }
            }

            return (_token, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage original, byte[]? body, List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null)
        {
            var content = new ByteArrayContent(body);
            content.Headers.Clear();
            foreach (var header in contentHeaders ?? [])
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Replays the stored response when a write request is retried with the same
/// <c>Idempotency-Key</c> (ADR-0004). An endpoint filter rather than a middleware because the key
/// is scoped per person, and authentication runs after the API middleware in the pipeline.
/// </summary>
/// <remarks>
/// One instance per endpoint lives as long as the app; the store is resolved per request so a
/// scoped (database) store works too. A replay repeats status, body and a few headers
/// (<see cref="ResponseCapture"/>), including the original <c>traceId</c>; endpoints that set
/// cookies or other headers must not rely on replays.
/// </remarks>
internal sealed class IdempotencyEndpointFilter(
    ResponseCapture capture,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var headerValues = httpContext.Request.Headers[IdempotencyDefaults.HeaderName];
        if (headerValues.Count == 0
            || !ApiRequests.IsApiV1Write(httpContext)
            || IdempotencyExclusions.IsEndpointExcluded(httpContext.GetEndpoint()))
        {
            return await next(context).ConfigureAwait(false);
        }

        var method = httpContext.Request.Method;
        var route = ApiRequests.RouteTemplate(httpContext);
        if (headerValues.Count != 1 || !Guid.TryParse(headerValues[0], out var idempotencyKey))
        {
            logger.KeyInvalid(method, route);
            ApiMetrics.IdempotencyOutcome("invalid_key");
            return ApiProblems.BadRequest(ErrorCodes.IdempotencyKeyInvalid, "Idempotency-Key must be a single UUID.");
        }

        var store = httpContext.RequestServices.GetRequiredService<IIdempotencyStore>();

        // Anonymous callers share one scope instead of an IP scope: a retry after a network change
        // must still match. Replaying needs the same random key and the same body, so nobody gets
        // an answer to a request they could not have sent themselves. The path ignores case like
        // routing; the fingerprint keeps the exact path.
        var caller = ApiRequests.PersonKey(httpContext.User) ?? "anonymous";
        var key = new IdempotencyRequestKey(caller, (httpContext.Request.Path.Value ?? string.Empty).ToUpperInvariant(), idempotencyKey);
        var fingerprint = await ComputeFingerprintAsync(httpContext.Request, httpContext.RequestAborted).ConfigureAwait(false);
        var begin = await store.TryBeginAsync(key, fingerprint, httpContext.RequestAborted).ConfigureAwait(false);

        switch (begin.Outcome)
        {
            case IdempotencyBeginOutcome.Completed:
                logger.Replayed(method, route, begin.Response!.StatusCode);
                ApiMetrics.IdempotencyOutcome("replayed");
                return new StoredResponseResult(begin.Response);
            case IdempotencyBeginOutcome.InProgress:
                logger.RequestInProgress(method, route);
                ApiMetrics.IdempotencyOutcome("in_progress");
                return ApiProblems.Conflict(ErrorCodes.IdempotencyRequestInProgress, "A request with this Idempotency-Key is still being processed.");
            case IdempotencyBeginOutcome.FingerprintMismatch:
                logger.KeyReused(method, route);
                ApiMetrics.IdempotencyOutcome("key_reused");
                return ApiProblems.RuleViolated(ErrorCodes.IdempotencyKeyReused, "This Idempotency-Key was already used for a different request.");
        }

        IdempotentResponse response;
        var handlerCompleted = false;
        try
        {
            response = await capture.CaptureAsync(context, next, () => handlerCompleted = true).ConfigureAwait(false);
        }
        catch when (handlerCompleted)
        {
            // The handler's effects are committed, only writing its result failed. Releasing the
            // key would let a retry repeat them; the reservation expires after the lease instead.
            ApiMetrics.IdempotencyOutcome("write_failed");
            throw;
        }
        catch
        {
            await TryAbandonAsync(store, key, begin.ReservationId, method, route).ConfigureAwait(false);
            ApiMetrics.IdempotencyOutcome("abandoned");
            throw;
        }

        if (IdempotencyExclusions.IsResponseExcluded(httpContext))
        {
            await TryAbandonAsync(store, key, begin.ReservationId, method, route).ConfigureAwait(false);
            ApiMetrics.IdempotencyOutcome("abandoned");
        }
        else
        {
            await StoreAsync(store, key, begin.ReservationId, response, method, route).ConfigureAwait(false);
        }

        // Sent only after storing: if the connection breaks now, the retry gets the stored answer
        // instead of running the handler (and its side effects) a second time.
        if (!response.Body.IsEmpty)
        {
            await httpContext.Response.Body.WriteAsync(response.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }

        return EmptyHttpResult.Instance;
    }

    private async Task StoreAsync(
        IIdempotencyStore store, IdempotencyRequestKey key, Guid reservationId, IdempotentResponse response, string method, string route)
    {
        // Server errors may be transient: the retry runs again instead of replaying the failure.
        if (response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            await TryAbandonAsync(store, key, reservationId, method, route).ConfigureAwait(false);
            ApiMetrics.IdempotencyOutcome("abandoned");
            return;
        }

        // A too large answer is stored without body (status, Location, ETag): the retry must not
        // repeat the write; the client reloads the resource instead.
        var tooLarge = response.Body.Length > IdempotencyDefaults.MaxStoredResponseBytes;
        var stored = tooLarge
            ? response with { Headers = WithoutContentType(response.Headers), Body = ReadOnlyMemory<byte>.Empty }
            : response;
        try
        {
            await store.CompleteAsync(key, reservationId, stored, CancellationToken.None).ConfigureAwait(false);
            ApiMetrics.IdempotencyOutcome(tooLarge ? "stored_without_body" : "stored");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The handler's effects are committed: the client still gets its answer. Like a failed
            // result write, the reservation is kept (until the lease ends) rather than released,
            // so an immediate retry cannot repeat the write.
            logger.StoreFailed(exception, method, route);
            ApiMetrics.IdempotencyOutcome("store_failed");
        }
    }

    private static Dictionary<string, string> WithoutContentType(IReadOnlyDictionary<string, string> headers) =>
        headers.Where(header => !string.Equals(header.Key, HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key, header => header.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// SHA-256 over method, path, query, <c>If-Match</c> and body: detects a reused key for a
    /// different request without keeping the body. The exact path counts (the key scope ignores
    /// case like routing), so resources whose ids differ in case are never confused. <c>If-Match</c>
    /// counts: a change against a newer version is a new request and needs a new key (422
    /// <c>key_reused</c> instead of replaying the stale 412).
    /// </summary>
    private static async Task<string> ComputeFingerprintAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.Body.CanSeek)
        {
            // ApiIdempotencyRegistration.UseIdempotencyBuffering must run before the endpoint.
            throw new InvalidOperationException("Request body buffering is not enabled for idempotent requests.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(
            $"{request.Method}\n{request.Path.Value}\n{request.QueryString.Value}\n{request.Headers.IfMatch}\n"));

        request.Body.Position = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int read;
            while ((read = await request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        request.Body.Position = 0;
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Best effort: a failing store must not replace the request's own outcome or exception.</summary>
    private async Task TryAbandonAsync(IIdempotencyStore store, IdempotencyRequestKey key, Guid reservationId, string method, string route)
    {
        try
        {
            await store.AbandonAsync(key, reservationId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.StoreFailed(exception, method, route);
        }
    }
}

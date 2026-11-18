namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Opt-out of the idempotency convention that <see cref="ApiV1Routes.MapApiV1"/> puts on the whole
/// group (ADR-0004).
/// </summary>
public static class IdempotencyExclusions
{
    private static readonly object DoNotStoreKey = new();

    /// <summary>
    /// No idempotency filter for this endpoint, e.g. sign-in: a replay would come without the
    /// session cookie, and the fingerprint would hash the password.
    /// </summary>
    public static TBuilder WithoutIdempotency<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(endpoint => endpoint.Metadata.Add(new WithoutIdempotencyMetadata()));
        return builder;
    }

    /// <summary>
    /// The current answer rejects the request before it ran (e.g. expired antiforgery token): it is
    /// not stored, so a retry with a fresh token and the same key runs normally.
    /// </summary>
    public static void DoNotStoreResponse(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Items[DoNotStoreKey] = true;
    }

    internal static bool IsResponseExcluded(HttpContext httpContext) => httpContext.Items.ContainsKey(DoNotStoreKey);

    // Checked per request: endpoint conventions run after the group convention that installs
    // the filter, so the filter cannot be left out when the endpoint is built.
    internal static bool IsEndpointExcluded(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<WithoutIdempotencyMetadata>() is not null;

    /// <summary>Endpoint metadata set by <see cref="WithoutIdempotency{TBuilder}"/>.</summary>
    private sealed class WithoutIdempotencyMetadata;
}

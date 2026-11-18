namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Endpoint convention for <c>Idempotency-Key</c> (ADR-0004).</summary>
public static class IdempotencyConventions
{
    /// <summary>
    /// Adds the idempotency filter to the write endpoints of a group or endpoint under
    /// <c>/api/v1</c>. Already applied to the whole group by <see cref="ApiV1Routes.MapApiV1"/>;
    /// applying it again (e.g. on a feature group) has no further effect.
    /// </summary>
    public static TBuilder WithIdempotency<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(endpoint =>
        {
            // Only the outermost application installs the filter: a second, nested filter would
            // see the key reserved by the first and answer 409 "in progress".
            if (endpoint.Metadata.OfType<IdempotencyMarker>().Any())
            {
                return;
            }

            endpoint.Metadata.Add(new IdempotencyMarker());
            var methods = endpoint.Metadata.OfType<IHttpMethodMetadata>().LastOrDefault()?.HttpMethods;
            if (methods is { Count: > 0 } && !methods.Any(ApiRequests.IsWriteMethod))
            {
                return; // Read-only endpoint: no filter overhead.
            }

            endpoint.FilterFactories.Add((factoryContext, next) =>
            {
                var filter = ActivatorUtilities.CreateInstance<IdempotencyEndpointFilter>(
                    factoryContext.ApplicationServices, new ResponseCapture(factoryContext.MethodInfo));
                return invocationContext => filter.InvokeAsync(invocationContext, next);
            });
        });
        return builder;
    }

    /// <summary>Endpoint metadata that marks where the convention was applied.</summary>
    private sealed class IdempotencyMarker;
}

using FoodAssistant.Server.Api;
using FoodAssistant.Server.Api.Idempotency;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Server.Composition;

/// <summary>Store and body buffering for <c>Idempotency-Key</c> (ADR-0004). Part of <see cref="ApiRegistration"/>.</summary>
internal static class ApiIdempotencyRegistration
{
    public static IServiceCollection AddApiIdempotency(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }

    /// <summary>
    /// Makes request bodies of idempotent write requests re-readable: model binding consumes the
    /// body before the endpoint filter runs, which then hashes it for the fingerprint.
    /// </summary>
    public static IApplicationBuilder UseIdempotencyBuffering(this IApplicationBuilder app) =>
        app.UseWhen(
            context => ApiRequests.IsApiV1Write(context) && context.Request.Headers.ContainsKey(IdempotencyDefaults.HeaderName),
            buffered => buffered.Use((context, next) =>
            {
                context.Request.EnableBuffering();
                return next(context);
            }));
}

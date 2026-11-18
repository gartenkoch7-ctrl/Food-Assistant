using FoodAssistant.Server.Api;
using FoodAssistant.Server.Api.Errors;

namespace FoodAssistant.Server.Composition;

/// <summary>ProblemDetails, exception handler and status code pages for API routes (ADR-0004). Part of <see cref="ApiRegistration"/>.</summary>
internal static class ApiErrorsRegistration
{
    public static IServiceCollection AddApiErrors(this IServiceCollection services)
    {
        // First writer wins: ours must come before the framework default added by AddProblemDetails.
        services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, ApiProblemDetailsWriter>());
        services.AddProblemDetails();
        services.Configure<ProblemDetailsOptions>(options =>
        {
            var previous = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                previous?.Invoke(context);
                ProblemDetailsEnricher.Enrich(context);
            };
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Runs in its own branch so the HTML error page of the web app stays untouched. The branch
    /// rejoins the main pipeline, so it wraps everything after it, including the endpoints.
    /// </summary>
    public static IApplicationBuilder UseApiErrors(this IApplicationBuilder app) =>
        app.UseWhen(ApiRequests.IsApi, api =>
        {
            api.UseExceptionHandler();
            api.UseStatusCodePages();
        });
}

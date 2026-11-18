using Microsoft.OpenApi;

namespace FoodAssistant.Server.Composition;

/// <summary>
/// OpenAPI document of <c>/api/v1</c>, served at <c>/openapi/v1.json</c>; the source of truth for
/// clients and checked by a snapshot test (ADR-0004). Part of <see cref="ApiRegistration"/>.
/// </summary>
internal static class ApiDocumentRegistration
{
    private const string DocumentName = "v1";

    public static IServiceCollection AddApiDocument(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Food-Assistant API",
                    Version = DocumentName,
                    License = new OpenApiLicense { Name = "MIT" },
                };

                // Each self-hosted server has its own address; clients use the one they connected to.
                // Without servers the document is also independent of the request host.
                document.Servers = [];
                return Task.CompletedTask;
            });
        });
}

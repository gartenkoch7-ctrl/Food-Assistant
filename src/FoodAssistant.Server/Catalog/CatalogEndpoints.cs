using FoodAssistant.Application.Catalog;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Results;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.Catalog;

/// <summary>
/// <c>/api/v1/households/{householdId}</c> → <c>/catalog/categories</c>, <c>/products</c>,
/// <c>/storage-locations</c>. Every member (also restricted ones) may read; changing needs the
/// member role (ADR-0002). Updates and deletes require <c>If-Match</c> (ADR-0004).
/// </summary>
internal static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var catalog = household.MapGroup("").WithTags("Catalog");

        catalog.MapGet("/catalog/categories", () => TypedResults.Ok(CategoryCatalog.All))
            .RequireAuthorization(AuthPolicies.HouseholdAny);

        MapProducts(catalog);
        MapLocations(catalog);
        return household;
    }

    private static void MapProducts(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/products", SearchAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        catalog.MapGet("/products/{productId:guid}", GetAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        catalog.MapGet("/products/by-barcode/{code}", FindByBarcodeAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        catalog.MapPost("/products", CreateAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        catalog.MapPut("/products/{productId:guid}", UpdateAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        catalog.MapDelete("/products/{productId:guid}", ArchiveAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
    }

    private static void MapLocations(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/storage-locations", ListLocationsAsync).RequireAuthorization(AuthPolicies.HouseholdAny);
        catalog.MapPost("/storage-locations", CreateLocationAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        catalog.MapPut("/storage-locations/{locationId:guid}", UpdateLocationAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
        catalog.MapDelete("/storage-locations/{locationId:guid}", DeleteLocationAsync).RequireAuthorization(AuthPolicies.HouseholdMember);
    }

    private static async Task<IResult> SearchAsync(
        Guid householdId,
        string? query,
        string? cursor,
        int? limit,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken) =>
        (await products.SearchAsync(householdId, query, cursor, limit, cancellationToken).ConfigureAwait(false)).ToHttpResult(http);

    private static async Task<IResult> GetAsync(
        Guid householdId,
        Guid productId,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken) =>
        (await products.GetAsync(householdId, productId, cancellationToken).ConfigureAwait(false)).ToHttpResult(http, p => p.Version);

    private static async Task<IResult> FindByBarcodeAsync(
        Guid householdId,
        string code,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken) =>
        (await products.FindByBarcodeAsync(householdId, code, cancellationToken).ConfigureAwait(false)).ToHttpResult(http, p => p.Version);

    private static async Task<IResult> CreateAsync(
        Guid householdId,
        CreateProductRequest request,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken) =>
        (await products.CreateAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, p => p.Version, p => $"/api/v1/households/{householdId}/products/{p.Id}");

    private static async Task<IResult> UpdateAsync(
        Guid householdId,
        Guid productId,
        UpdateProductRequest request,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await products.UpdateAsync(householdId, productId, expected, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, p => p.Version);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid householdId,
        Guid productId,
        HttpContext http,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await products.ArchiveAsync(householdId, productId, expected, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, p => p.Version);
    }

    private static async Task<IResult> ListLocationsAsync(
        Guid householdId,
        StorageLocationService locations,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await locations.ListAsync(householdId, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> CreateLocationAsync(
        Guid householdId,
        StorageLocationRequest request,
        HttpContext http,
        StorageLocationService locations,
        CancellationToken cancellationToken) =>
        (await locations.CreateAsync(householdId, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, l => l.Version, l => $"/api/v1/households/{householdId}/storage-locations/{l.Id}");

    private static async Task<IResult> UpdateLocationAsync(
        Guid householdId,
        Guid locationId,
        StorageLocationRequest request,
        HttpContext http,
        StorageLocationService locations,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await locations.UpdateAsync(householdId, locationId, expected, request, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, l => l.Version);
    }

    private static async Task<IResult> DeleteLocationAsync(
        Guid householdId,
        Guid locationId,
        HttpContext http,
        StorageLocationService locations,
        CancellationToken cancellationToken)
    {
        if (!IfMatch.TryRead(http, required: true, out var expected, out var problem))
        {
            return problem!;
        }

        return (await locations.DeleteAsync(householdId, locationId, expected, cancellationToken).ConfigureAwait(false))
            .ToHttpResult(http, l => l.Version);
    }
}

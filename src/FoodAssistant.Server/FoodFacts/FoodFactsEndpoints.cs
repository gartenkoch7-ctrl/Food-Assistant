using System.Globalization;
using FoodAssistant.Application.FoodFacts;
using FoodAssistant.Contracts.FoodFacts;
using FoodAssistant.Server.Api.Errors;
using FoodAssistant.Server.Auth;

namespace FoodAssistant.Server.FoodFacts;

/// <summary>
/// <c>GET /api/v1/households/{householdId}/food-facts/{barcode}</c>: product suggestion from Open
/// Food Facts for a scanned barcode. Every role may look up (it reads no household data); the
/// household is only part of the route for the access check.
/// </summary>
internal static class FoodFactsEndpoints
{
    public static RouteGroupBuilder MapFoodFactsEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        household.MapGet("/food-facts/{barcode}", LookupAsync)
            .WithTags("FoodFacts")
            .RequireAuthorization(AuthPolicies.HouseholdAny)
            .Produces<FoodFactsProductDto>();
        return household;
    }

    private static async Task<IResult> LookupAsync(
        string barcode,
        FoodFactsService service,
        IFoodFactsCategoryResolver categories,
        CancellationToken cancellationToken)
    {
        if (!IsValidBarcode(barcode))
        {
            return ApiProblems.BadRequest(FoodFactsErrorCodes.InvalidBarcode, "A barcode has 8, 12, 13 or 14 digits.");
        }

        var lookup = await service.LookupAsync(barcode, cancellationToken).ConfigureAwait(false);
        return lookup switch
        {
            { Status: FoodFactsStatus.Found, Product: { } product } => TypedResults.Ok(
                FoodFactsSuggestion.ToDto(product, CultureInfo.CurrentUICulture.Name, categories, lookup.Stale)),
            { Status: FoodFactsStatus.NotFound } => ApiProblems.NotFound(FoodFactsErrorCodes.NotFound),
            _ => ApiProblems.Problem(
                StatusCodes.Status503ServiceUnavailable,
                FoodFactsErrorCodes.Unavailable,
                "Open Food Facts is not available."),
        };
    }

    // EAN-8, UPC-A (12), EAN-13 and GTIN-14; ASCII digits only so nothing odd reaches the URL.
    private static bool IsValidBarcode(string barcode) =>
        barcode.Length is 8 or 12 or 13 or 14 && barcode.All(char.IsAsciiDigit);
}

using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.FoodFacts;

namespace FoodAssistant.Client.FoodFacts;

/// <summary>The barcode lookup endpoint over <see cref="ApiHttpClient"/>; the barcode is escaped into the route.</summary>
public sealed class FoodFactsClient(ApiHttpClient api) : IFoodFactsClient
{
    public Task<ApiResult<FoodFactsProductDto>> LookupAsync(Guid householdId, string barcode, CancellationToken cancellationToken) =>
        api.GetAsync<FoodFactsProductDto>(ApiPaths.Household(householdId, $"food-facts/{ApiPaths.Escape(barcode)}"), cancellationToken);
}

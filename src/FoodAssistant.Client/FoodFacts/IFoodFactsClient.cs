using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.FoodFacts;

namespace FoodAssistant.Client.FoodFacts;

/// <summary>
/// Product suggestions from Open Food Facts, looked up by the server (the app never talks to Open
/// Food Facts itself). Failures use the codes of <see cref="FoodFactsErrorCodes"/>.
/// </summary>
public interface IFoodFactsClient
{
    Task<ApiResult<FoodFactsProductDto>> LookupAsync(Guid householdId, string barcode, CancellationToken cancellationToken);
}

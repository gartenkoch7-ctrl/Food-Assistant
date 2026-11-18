namespace FoodAssistant.Application.FoodFacts;

/// <summary>The external Open Food Facts API. Network problems never throw; they yield <see cref="FoodFactsStatus.Unavailable"/>.</summary>
public interface IFoodFactsApi
{
    Task<FoodFactsLookup> FetchAsync(string barcode, CancellationToken cancellationToken);
}

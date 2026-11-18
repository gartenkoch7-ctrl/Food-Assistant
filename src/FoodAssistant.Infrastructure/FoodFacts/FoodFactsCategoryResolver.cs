using FoodAssistant.Application.FoodFacts;

namespace FoodAssistant.Infrastructure.FoodFacts;

internal sealed class FoodFactsCategoryResolver : IFoodFactsCategoryResolver
{
    public string? CategoryOf(IReadOnlyList<string> categoryTags) => FoodFactsCategoryMap.CategoryOf(categoryTags);
}

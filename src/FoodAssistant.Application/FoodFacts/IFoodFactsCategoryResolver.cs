namespace FoodAssistant.Application.FoodFacts;

/// <summary>Maps Open Food Facts category tags to a key of the fixed product category list.</summary>
public interface IFoodFactsCategoryResolver
{
    /// <summary>The category key, or null when no tag is recognised.</summary>
    string? CategoryOf(IReadOnlyList<string> categoryTags);
}

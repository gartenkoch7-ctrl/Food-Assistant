namespace FoodAssistant.Application.FoodFacts;

/// <summary>How long cached answers stay fresh. Misses are short so newly added products are found soon.</summary>
internal static class FoodFactsCachePolicy
{
    public static readonly TimeSpan FoundLifetime = TimeSpan.FromDays(30);

    public static readonly TimeSpan NotFoundLifetime = TimeSpan.FromDays(2);
}

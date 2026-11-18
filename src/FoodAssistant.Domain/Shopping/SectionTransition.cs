namespace FoodAssistant.Domain.Shopping;

/// <summary>How often, during one shopping trip, an entry of <paramref name="To"/> was checked off right after one of <paramref name="From"/>.</summary>
/// <param name="From">Section key of the previous check-off.</param>
/// <param name="To">Section key of the following check-off.</param>
/// <param name="Count">Number of times this happened.</param>
public sealed record SectionTransition(string From, string To, int Count);

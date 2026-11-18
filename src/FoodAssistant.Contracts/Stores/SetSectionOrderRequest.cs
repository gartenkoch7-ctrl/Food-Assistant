namespace FoodAssistant.Contracts.Stores;

/// <summary>Places sections of a store by hand.</summary>
/// <param name="Sections">
/// Known section keys in the wanted order, each at most once. Sections left out follow in the
/// learned order.
/// </param>
public sealed record SetSectionOrderRequest(IReadOnlyList<string> Sections);

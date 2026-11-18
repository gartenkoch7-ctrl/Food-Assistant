namespace FoodAssistant.Contracts.Stores;

/// <summary>The order in which the sections of a store are shown on a shopping list.</summary>
/// <param name="StoreId">The store, or <see langword="null"/> for the default order without a store.</param>
/// <param name="Sections">All section keys in display order.</param>
/// <param name="HasManualOrder">
/// Whether someone placed sections by hand; they come first, the rest follows the order learned
/// from check-offs.
/// </param>
public sealed record SectionOrderDto(Guid? StoreId, IReadOnlyList<string> Sections, bool HasManualOrder);

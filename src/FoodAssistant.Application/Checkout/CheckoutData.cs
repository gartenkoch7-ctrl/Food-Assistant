using FoodAssistant.Domain.Checkout;

namespace FoodAssistant.Application.Checkout;

/// <summary>What completing a list reads: its checked entries, their products and the household's locations.</summary>
/// <param name="ListId">The list.</param>
/// <param name="Items">Checked, not deleted entries in list order.</param>
/// <param name="Products">Requested products that exist and are not archived, by id.</param>
/// <param name="Locations">All storage locations in display order.</param>
public sealed record CheckoutData(
    Guid ListId,
    IReadOnlyList<CheckoutItem> Items,
    IReadOnlyDictionary<Guid, CheckoutProduct> Products,
    IReadOnlyList<LocationOption> Locations);

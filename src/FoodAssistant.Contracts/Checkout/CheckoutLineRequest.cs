using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.Checkout;

/// <summary>
/// What happens with one bought entry: <paramref name="Skip"/> removes it from the list without a
/// batch; otherwise a batch of <paramref name="ProductId"/> or of <paramref name="NewProduct"/>
/// (exactly one of them) is added. In a freezer a package date means bought frozen food; without
/// date or with <paramref name="SelfFrozen"/> the batch is frozen at home from today (F5).
/// </summary>
/// <param name="ItemId">Shopping list entry.</param>
/// <param name="ItemVersion">Version the client saw; a changed entry is not booked (reported as stale).</param>
/// <param name="Skip">Remove the entry without adding a batch.</param>
/// <param name="ProductId">Existing product.</param>
/// <param name="NewProduct">Product to create instead.</param>
/// <param name="Quantity">Amount of the batch.</param>
/// <param name="StorageLocationId">Location of the batch.</param>
/// <param name="ExpiresOn">Printed date, or null.</param>
/// <param name="ExpiryKind">Kind of the printed date; <c>none</c> exactly without date.</param>
/// <param name="SelfFrozen">Frozen at home although a package date is given.</param>
public sealed record CheckoutLineRequest(
    Guid ItemId,
    long ItemVersion,
    bool Skip,
    Guid? ProductId,
    NewProductLine? NewProduct,
    QuantityDto? Quantity,
    Guid? StorageLocationId,
    DateOnly? ExpiresOn,
    ExpiryKindDto ExpiryKind,
    bool SelfFrozen);

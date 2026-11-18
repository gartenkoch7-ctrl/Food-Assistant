using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.UI.Checkout;

/// <summary>Editable state of one bought entry on the checkout page, starting from the server's suggestion.</summary>
public sealed class CheckoutLine
{
    public CheckoutLine(CheckoutEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ItemId = entry.ItemId;
        ItemVersion = entry.ItemVersion;
        Text = entry.Text;
        IsFreeText = entry.ProductId is null;
        Reassignable = IsFreeText;
        ProductId = entry.ProductId;
        Name = entry.Text;
        Quantity = entry.Quantity;
        QuantityIsDefault = !entry.QuantityFromEntry;
        LocationId = entry.StorageLocationId;
        LocationIsDefault = entry.ProductId is null;
        Date = entry.ExpiresOn;
        Kind = entry.ExpiryKind;
        FromRule = entry.ExpiryFromRule;
        Suggested = entry.ExpiresOn is not null && !entry.ExpiryFromRule;
    }

    public Guid ItemId { get; }

    public long ItemVersion { get; }

    /// <summary>Text of the list entry.</summary>
    public string Text { get; }

    /// <summary>The entry has no (active) product: it must be assigned one or skipped.</summary>
    public bool IsFreeText { get; }

    /// <summary>
    /// The product of this line is chosen on the page (free text, or a product archived meanwhile), so the
    /// row offers "other product" and shows the entry text.
    /// </summary>
    public bool Reassignable { get; private set; }

    public Guid? ProductId { get; set; }

    /// <summary>Product name, or the entry text while no product is assigned.</summary>
    public string Name { get; set; }

    public QuantityDto Quantity { get; set; }

    /// <summary>
    /// The amount is the server's default ("1 × default unit", "1 piece" for free text): neither from the
    /// list entry nor edited. Only then does another product bring its own default unit.
    /// </summary>
    public bool QuantityIsDefault { get; set; }

    public Guid? LocationId { get; set; }

    /// <summary>
    /// Free text whose location is still the generic suggestion (first location); an assigned product
    /// may then bring a location of its category. False once the person chose a location.
    /// </summary>
    public bool LocationIsDefault { get; set; }

    public DateOnly? Date { get; set; }

    /// <summary>Kind of <see cref="Date"/>; <see cref="ExpiryKindDto.None"/> without date.</summary>
    public ExpiryKindDto Kind { get; set; }

    /// <summary>
    /// <see cref="Date"/> is the server's rule "frozen" (frozen at home). It is not sent back: without a
    /// package date the server freezes the batch by the same rule (F5).
    /// </summary>
    public bool FromRule { get; set; }

    /// <summary>
    /// <see cref="Date"/> is the untouched suggestion from the last purchase. That purchase may have been
    /// frozen goods (or fresh ones), so the date only fits the kind of place it was suggested for.
    /// </summary>
    public bool Suggested { get; set; }

    /// <summary>Frozen at home although a package date is given; only in a freezer.</summary>
    public bool SelfFrozen { get; set; }

    public bool Skip { get; set; }

    public CheckoutLine Copy() => (CheckoutLine)MemberwiseClone();

    /// <summary>
    /// Assigns a (different) product. Suggestions made for the previous product (its last purchase, its
    /// rule date, its default unit) do not fit the new one and go; what the person entered stays.
    /// </summary>
    public void AssignProduct(Guid productId, string name, string? defaultUnit)
    {
        (ProductId, Name) = (productId, name);
        if (QuantityIsDefault && !string.IsNullOrEmpty(defaultUnit))
        {
            Quantity = new QuantityDto(1m, defaultUnit);
        }

        DropSuggestedDate();
    }

    /// <summary>Removes the product so another one can be picked; its suggested date goes with it.</summary>
    public void ClearProduct()
    {
        (ProductId, Name, Reassignable) = (null, Text, true);
        DropSuggestedDate();
    }

    private void DropSuggestedDate()
    {
        if (Suggested || FromRule)
        {
            (Date, Kind, Suggested, FromRule) = (null, ExpiryKindDto.None, false, false);
        }
    }

    /// <summary>
    /// Moves the line. A date the person did not enter (rule "frozen", last purchase) goes when the line
    /// moves between a freezer and any other place: a freezer date must never end up in the fridge.
    /// </summary>
    /// <param name="locationId">New location.</param>
    /// <param name="freezer">The new location is a freezer.</param>
    /// <param name="fromFreezer">The previous location was a freezer.</param>
    public void MoveTo(Guid? locationId, bool freezer, bool fromFreezer)
    {
        LocationId = locationId;
        if (!freezer)
        {
            SelfFrozen = false;
        }

        if (freezer != fromFreezer && (FromRule || Suggested))
        {
            (Date, Kind, FromRule, Suggested) = (null, ExpiryKindDto.None, false, false);
        }
    }

    /// <summary>Takes the values of an edited copy.</summary>
    public void Apply(CheckoutLine edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        LocationIsDefault = edited.LocationIsDefault;
        (Quantity, QuantityIsDefault, LocationId, Date, Kind, FromRule, Suggested, SelfFrozen) =
            (edited.Quantity, edited.QuantityIsDefault, edited.LocationId, edited.Date, edited.Kind, edited.FromRule, edited.Suggested, edited.SelfFrozen);
    }

    public CheckoutLineRequest ToRequest()
    {
        if (Skip)
        {
            return new CheckoutLineRequest(ItemId, ItemVersion, true, null, null, null, null, null, ExpiryKindDto.None, false);
        }

        var packageDate = FromRule ? null : Date;
        return new CheckoutLineRequest(
            ItemId,
            ItemVersion,
            false,
            ProductId,
            null,
            Quantity,
            LocationId,
            packageDate,
            packageDate is null ? ExpiryKindDto.None : Kind,
            packageDate is not null && SelfFrozen);
    }
}

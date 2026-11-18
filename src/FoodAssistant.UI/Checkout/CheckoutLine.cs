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
        ProductId = entry.ProductId;
        Name = entry.Text;
        Quantity = entry.Quantity;
        LocationId = entry.StorageLocationId;
        Date = entry.ExpiresOn;
        Kind = entry.ExpiryKind;
        FromRule = entry.ExpiryFromRule;
    }

    public Guid ItemId { get; }

    public long ItemVersion { get; }

    /// <summary>Text of the list entry.</summary>
    public string Text { get; }

    /// <summary>The entry has no (active) product: it must be assigned one or skipped.</summary>
    public bool IsFreeText { get; }

    public Guid? ProductId { get; set; }

    /// <summary>Product name, or the entry text while no product is assigned.</summary>
    public string Name { get; set; }

    public QuantityDto Quantity { get; set; }

    public Guid? LocationId { get; set; }

    public DateOnly? Date { get; set; }

    /// <summary>Kind of <see cref="Date"/>; <see cref="ExpiryKindDto.None"/> without date.</summary>
    public ExpiryKindDto Kind { get; set; }

    /// <summary>
    /// <see cref="Date"/> is the server's rule "frozen" (frozen at home). It is not sent back: without a
    /// package date the server freezes the batch by the same rule (F5).
    /// </summary>
    public bool FromRule { get; set; }

    /// <summary>Frozen at home although a package date is given; only in a freezer.</summary>
    public bool SelfFrozen { get; set; }

    public bool Skip { get; set; }

    public CheckoutLine Copy() => (CheckoutLine)MemberwiseClone();

    /// <summary>Moves the line; a rule date belongs to the freezer and goes when leaving it.</summary>
    public void MoveTo(Guid? locationId, bool freezer)
    {
        LocationId = locationId;
        if (!freezer)
        {
            SelfFrozen = false;
            if (FromRule)
            {
                (Date, Kind, FromRule) = (null, ExpiryKindDto.None, false);
            }
        }
    }

    /// <summary>Takes the values of an edited copy.</summary>
    public void Apply(CheckoutLine edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        (Quantity, LocationId, Date, Kind, FromRule, SelfFrozen) =
            (edited.Quantity, edited.LocationId, edited.Date, edited.Kind, edited.FromRule, edited.SelfFrozen);
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

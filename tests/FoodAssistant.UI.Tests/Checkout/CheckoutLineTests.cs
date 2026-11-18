using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Checkout;

namespace FoodAssistant.UI.Tests.Checkout;

/// <summary>State rules of a checkout line when products change.</summary>
public sealed class CheckoutLineTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    private static CheckoutLine Line(Guid? productId, QuantityDto quantity, bool fromEntry, DateOnly? date = null) =>
        new(new CheckoutEntryDto(
            Guid.CreateVersion7(), 1, productId, "Eintrag", null, quantity, Guid.CreateVersion7(), date,
            date is null ? ExpiryKindDto.None : ExpiryKindDto.BestBefore, false, fromEntry));

    [Fact]
    public void AssignOther_TakesTheNewProductsUnit_WhileTheAmountIsTheDefault()
    {
        var line = Line(null, new QuantityDto(1m, "piece"), fromEntry: false);

        line.AssignProduct(Guid.CreateVersion7(), "Hafer", "l");
        line.ClearProduct();
        line.AssignProduct(Guid.CreateVersion7(), "Mehl", "kg");

        Assert.Equal((new QuantityDto(1m, "kg"), "Mehl"), (line.Quantity, line.Name));
    }

    [Fact]
    public void AssignOther_KeepsAnAmountFromTheEntry()
    {
        var line = Line(null, new QuantityDto(2m, "piece"), fromEntry: true);

        line.AssignProduct(Guid.CreateVersion7(), "Mehl", "kg");

        Assert.Equal(new QuantityDto(2m, "piece"), line.Quantity);
    }

    [Fact]
    public void ArchivedProduct_RePicked_DropsTheOldProductsSuggestedDate()
    {
        var line = Line(Guid.CreateVersion7(), new QuantityDto(1m, "l"), fromEntry: false, date: Today.AddDays(7));

        line.ClearProduct();
        line.AssignProduct(Guid.CreateVersion7(), "Butter", "package");

        var request = line.ToRequest();
        Assert.Equal(((DateOnly?)null, ExpiryKindDto.None, new QuantityDto(1m, "package")), (request.ExpiresOn, request.ExpiryKind, request.Quantity));
        Assert.Equal("Eintrag", line.Text);
    }

    [Fact]
    public void PersonsOwnDate_StaysWhenTheProductChanges()
    {
        var line = Line(Guid.CreateVersion7(), new QuantityDto(1m, "l"), fromEntry: false);
        (line.Date, line.Kind) = (Today.AddDays(3), ExpiryKindDto.UseBy);

        line.AssignProduct(Guid.CreateVersion7(), "Butter", "package");

        Assert.Equal((Today.AddDays(3), ExpiryKindDto.UseBy), (line.ToRequest().ExpiresOn!.Value, line.ToRequest().ExpiryKind));
    }
}

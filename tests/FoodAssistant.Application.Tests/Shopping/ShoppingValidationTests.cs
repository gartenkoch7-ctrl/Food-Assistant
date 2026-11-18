using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Tests.Shopping;

public sealed class ShoppingValidationTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("Weekly", true)]
    [InlineData(null, false)]
    public void ListName_EmptyAllowed_NullRequired(string? name, bool valid) =>
        Assert.Equal(valid, ShoppingValidation.ValidateListName(name).Count == 0);

    [Fact]
    public void ListName_TooLong_AfterTrimming()
    {
        Assert.Empty(ShoppingValidation.ValidateListName($"  {new string('x', 80)}  "));
        Assert.Equal(["too_long"], ShoppingValidation.ValidateListName(new string('x', 81))["name"]);
    }

    [Fact]
    public void NewItem_FreeText_ChecksTextSectionQuantityAndNote()
    {
        var valid = new CreateShoppingItemRequest(null, null, "Bread", new QuantityDto(0.5m, UnitCodes.Kilogram), "fresh", StoreSections.Bakery);

        Assert.Empty(ShoppingValidation.ValidateNewItem(valid));
        Assert.Equal(["required"], ShoppingValidation.ValidateNewItem(valid with { Text = "  " })["text"]);
        Assert.Equal(["too_long"], ShoppingValidation.ValidateNewItem(valid with { Text = new string('t', 201) })["text"]);
        Assert.Equal(["unknown"], ShoppingValidation.ValidateNewItem(valid with { SectionKey = "Bakery" })["sectionKey"]);
        Assert.Equal(["too_long"], ShoppingValidation.ValidateNewItem(valid with { Note = new string('n', 501) })["note"]);
        Assert.Empty(ShoppingValidation.ValidateNewItem(valid with { SectionKey = null, Note = "   " }));
    }

    [Fact]
    public void NewItem_Product_IgnoresTextAndSection()
    {
        var request = new CreateShoppingItemRequest(null, Guid.CreateVersion7(), null, null, null, "unknown");

        Assert.Empty(ShoppingValidation.ValidateNewItem(request));
    }

    [Theory]
    [InlineData("0", "g", "invalid")]
    [InlineData("-1", "g", "invalid")]
    [InlineData("1.0005", "g", "invalid")]
    [InlineData("1000000000", "g", "invalid")]
    [InlineData("1", "bucket", "unknown_unit")]
    [InlineData("1", "G", "unknown_unit")]
    public void Quantity_MustBePositiveFitTheColumnAndHaveAKnownUnit(string value, string unit, string error)
    {
        var request = new CreateShoppingItemRequest(null, null, "Flour", new QuantityDto(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), unit), null, null);

        Assert.Equal([error], ShoppingValidation.ValidateNewItem(request)["quantity"]);
    }

    [Fact]
    public void Change_RequiresPositiveBaseVersionAndKnownSection()
    {
        var valid = new ChangeShoppingItemRequest(1, true, null, null, "Bread", StoreSections.Bakery);

        Assert.Empty(ShoppingValidation.ValidateChange(valid));
        Assert.Equal(["invalid"], ShoppingValidation.ValidateChange(valid with { BaseVersion = 0 })["baseVersion"]);
        Assert.Equal(["required"], ShoppingValidation.ValidateChange(valid with { SectionKey = "" })["sectionKey"]);
        Assert.Equal(["unknown"], ShoppingValidation.ValidateChange(valid with { SectionKey = "aisle" })["sectionKey"]);
        Assert.Equal(["required"], ShoppingValidation.ValidateChange(valid with { Text = "" })["text"]);
    }

    [Fact]
    public void InternalItem_FollowsTheSameRules()
    {
        var item = new NewShoppingItem(
            Guid.CreateVersion7(), null, "Milk", new Quantity(1m, Unit.Litre), null, StoreSections.DairyChilled, ShoppingItemOrigin.MinimumStock, null);

        Assert.True(ShoppingValidation.IsValid(item));
        Assert.False(ShoppingValidation.IsValid(item with { Text = " " }));
        Assert.False(ShoppingValidation.IsValid(item with { SectionKey = "dairy" }));
        Assert.False(ShoppingValidation.IsValid(item with { Quantity = Quantity.Zero(Unit.Litre) }));
    }

    [Fact]
    public void NormalizeNote_TrimsAndDropsBlankNotes()
    {
        Assert.Null(ShoppingValidation.NormalizeNote(" \t"));
        Assert.Equal("ripe", ShoppingValidation.NormalizeNote(" ripe "));
    }
}

using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingItemRowTests : ShoppingTestContext
{
    private ShoppingItemChangeResult? _changed;
    private ApiError? _failed;
    private ShoppingItemDto? _opened;

    private IRenderedComponent<ShoppingItemRow> RenderRow(ShoppingItemDto item) =>
        Render<ShoppingItemRow>(p => p
            .Add(c => c.Item, item)
            .Add(c => c.Changed, EventCallback.Factory.Create<ShoppingItemChangeResult>(this, r => _changed = r))
            .Add(c => c.Failed, EventCallback.Factory.Create<ApiError>(this, e => _failed = e))
            .Add(c => c.Open, EventCallback.Factory.Create<ShoppingItemDto>(this, i => _opened = i)));

    [Fact]
    public void ShowsTextQuantityAndNote()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch", quantity: new QuantityDto(2, "piece"), note: "bio");

        var cut = RenderRow(item);

        Assert.Equal("Milch", cut.Find(".fa-list-row-name").TextContent);
        Assert.Equal("2 Stück · bio", cut.Find(".fa-list-row-secondary").TextContent);
    }

    [Theory]
    [InlineData(ShoppingItemOriginDto.MinimumStock, true)]
    [InlineData(ShoppingItemOriginDto.Recurring, true)]
    [InlineData(ShoppingItemOriginDto.Manual, false)]
    [InlineData(ShoppingItemOriginDto.MealPlan, false)]
    public void TheAutomaticLabel_IsPlainText_OnlyForMinimumStockAndRecurring(ShoppingItemOriginDto origin, bool labelled)
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch", origin: origin);

        var cut = RenderRow(item);

        Assert.Equal(labelled, cut.Markup.Contains("automatisch", StringComparison.Ordinal));
    }

    [Fact]
    public void English_NamesTheButtonsAfterTheItem()
    {
        UseCulture("en");
        var item = AddItem(Guid.CreateVersion7(), "Milk", origin: ShoppingItemOriginDto.Recurring);

        var cut = RenderRow(item);

        Assert.NotNull(cut.Find("button[aria-label='Check off Milk']"));
        Assert.NotNull(cut.Find("button[aria-label='Edit Milk']"));
        Assert.Contains("automatic", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Checking_SendsTheFullTargetState_AndReportsTheServerAnswer()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch", "dairy_chilled", quantity: new QuantityDto(2, "l"), note: "bio");
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(_changed));
        var request = Assert.Single(Shopping.Changes);
        Assert.Equal(new ChangeShoppingItemRequest(item.Version, true, item.Quantity, "bio", "Milch", "dairy_chilled"), request);
        Assert.True(_changed!.Item.Checked);
        Assert.Null(_failed);
    }

    [Fact]
    public void Checking_FlipsTheRowAtOnce_WhileTheServerIsStillAnswering()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch");
        Shopping.HoldChange = new TaskCompletionSource();
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        // Not disabled: a disabled button would drop the keyboard focus. A second press is ignored instead.
        var flipped = cut.Find("button[aria-label='Milch zurücknehmen']");
        Assert.False(flipped.HasAttribute("disabled"));
        flipped.Click();
        Assert.Null(_changed);

        Shopping.HoldChange.SetResult();
        cut.WaitForAssertion(() => Assert.NotNull(_changed));
        Assert.Single(Shopping.Changes);
    }

    [Fact]
    public void RefusedAsRestrictedChange_IsRetriedOnceOnTheFreshState()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch", "dairy_chilled");
        var stale = item with { Note = "veraltet", Version = 1 };
        Shopping.Items[0] = item with { Note = "von anderen", Version = 8 };
        Shopping.FailChangeWith = FakeAccountClient.Error(403, "shopping.restricted_change");
        Shopping.FailChangeOnce = true;
        var cut = RenderRow(stale);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(_changed));
        Assert.Null(_failed);
        Assert.Equal(2, Shopping.Changes.Count);
        Assert.Equal(new ChangeShoppingItemRequest(8, true, null, "von anderen", "Milch", "dairy_chilled"), Shopping.Changes[1]);
    }

    [Fact]
    public void RefusedTwice_IsReportedAfterTheOneRetry()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch");
        Shopping.FailChangeWith = FakeAccountClient.Error(403, "shopping.restricted_change");
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(_failed));
        Assert.Equal(2, Shopping.Changes.Count);
    }

    [Fact]
    public void FailingCheck_PutsTheRowBack_AndRaisesTheError()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch");
        Shopping.FailChangeWith = FakeAccountClient.Error(409, "shopping.unknown_version");
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(_failed));
        Assert.Equal("shopping.unknown_version", _failed!.ErrorCode);
        var button = cut.Find("button[aria-label='Milch abhaken']");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Null(_changed);
    }

    [Fact]
    public void CheckedItem_OffersToTakeItBack()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch", isChecked: true);
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch zurücknehmen']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(_changed));
        Assert.False(Shopping.Changes[0].Checked);
        Assert.False(_changed!.Item.Checked);
    }

    [Fact]
    public void EditButton_RaisesOpen()
    {
        var item = AddItem(Guid.CreateVersion7(), "Milch");
        var cut = RenderRow(item);

        cut.Find("button[aria-label='Milch bearbeiten']").Click();

        Assert.Equal(item, _opened);
    }
}

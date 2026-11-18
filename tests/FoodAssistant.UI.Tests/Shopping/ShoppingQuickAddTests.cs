using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingQuickAddTests : ShoppingTestContext
{
    private readonly List<ShoppingItemDto> _added = [];

    private IRenderedComponent<ShoppingQuickAdd> RenderAdd(Guid listId) =>
        Render<ShoppingQuickAdd>(p => p
            .Add(c => c.ListId, listId)
            .Add(c => c.Added, EventCallback.Factory.Create<ShoppingItemDto>(this, _added.Add)));

    [Fact]
    public void TheField_IsNeverDisabled_WhileARequestRuns_AndRefocusesAfterwards()
    {
        var list = AddList();
        var cut = RenderAdd(list.Id);

        cut.Find("form input").Input("Eier");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(_added));
        Assert.False(cut.Find("form input").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("form button[disabled]"));
        // MudTextField.FocusAsync goes through JS interop; the loose mode records it.
        Assert.Contains(JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RetryAfterAFailure_SendsTheSameItemId_SoNothingIsCreatedTwice()
    {
        var list = AddList();
        var cut = RenderAdd(list.Id);
        Shopping.FailWith = FakeAccountClient.Error(0, ClientErrorCodes.Network);

        cut.Find("form input").Input("Eier");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Single(Shopping.AddRequests));
        Shopping.FailWith = null;
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(_added));
        Assert.Equal(2, Shopping.AddRequests.Count);
        Assert.Equal(Shopping.AddRequests[0].Id, Shopping.AddRequests[1].Id);
        Assert.Single(Shopping.Items);
    }

    [Fact]
    public void TextTypedWhileTheRequestRuns_IsNotCleared()
    {
        var list = AddList();
        var cut = RenderAdd(list.Id);
        var hold = new TaskCompletionSource();
        Shopping.HoldAdd = hold;

        cut.Find("form input").Input("Eier");
        cut.Find("form").Submit();
        cut.Find("form input").Input("Eier und Milch");
        hold.SetResult();

        cut.WaitForAssertion(() => Assert.Single(_added));
        Assert.Equal("Eier und Milch", cut.Find("form input").GetAttribute("value"));
    }

    [Fact]
    public void RetryOnAnotherList_GetsANewItemId()
    {
        var first = AddList();
        var second = AddList("Wochenende", isDefault: false);
        var cut = RenderAdd(first.Id);
        Shopping.FailWith = FakeAccountClient.Error(0, ClientErrorCodes.Network);

        cut.Find("form input").Input("Eier");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Single(Shopping.AddRequests));
        Shopping.FailWith = null;
        cut.Render(p => p.Add(c => c.ListId, second.Id));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(_added));
        Assert.NotEqual(Shopping.AddRequests[0].Id, Shopping.AddRequests[1].Id);
        Assert.Equal(second.Id, _added[0].ListId);
    }

    [Fact]
    public void ChangedText_GetsANewItemId()
    {
        var list = AddList();
        var cut = RenderAdd(list.Id);
        Shopping.FailWith = FakeAccountClient.Error(0, ClientErrorCodes.Network);

        cut.Find("form input").Input("Eier");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Single(Shopping.AddRequests));
        Shopping.FailWith = null;
        cut.Find("form input").Input("Milch");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(_added));
        Assert.NotEqual(Shopping.AddRequests[0].Id, Shopping.AddRequests[1].Id);
        Assert.Equal("Milch", Shopping.Items[0].Text);
    }

    [Fact]
    public void ThePickerButton_ControlsThePicker()
    {
        var list = AddList();
        var cut = RenderAdd(list.Id);
        var button = cut.FindAll("button").First(b => b.TextContent.Contains("Produkt wählen", StringComparison.Ordinal));

        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Null(button.GetAttribute("aria-controls"));
        button.Click();

        Assert.Equal("true", cut.FindAll("button").First(b => b.TextContent.Contains("Produkt wählen", StringComparison.Ordinal)).GetAttribute("aria-expanded"));
        Assert.NotNull(cut.Find("#fa-quick-add-picker"));
        Assert.Equal("fa-quick-add-picker", cut.FindAll("button").First(b => b.TextContent.Contains("Produkt wählen", StringComparison.Ordinal)).GetAttribute("aria-controls"));
    }
}

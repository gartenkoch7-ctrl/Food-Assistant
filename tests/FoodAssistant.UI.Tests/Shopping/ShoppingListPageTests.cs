using Bunit;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.UI.Catalog;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingListPageTests : ShoppingTestContext
{
    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    private IRenderedComponent<ShoppingListPage> RenderList(Guid listId) =>
        Render<ShoppingListPage>(p => p.Add(c => c.ListId, listId));

    private static IReadOnlyList<string> Headings(IRenderedComponent<ShoppingListPage> cut) =>
        [.. cut.FindAll("h2").Select(h => h.TextContent.Trim())];

    [Fact]
    public void ListWithoutStore_GroupsBySection_InTheDefaultOrder()
    {
        var list = AddList();
        AddItem(list.Id, "Milch", "dairy_chilled");
        AddItem(list.Id, "Äpfel", "produce");
        AddItem(list.Id, "Joghurt", "dairy_chilled");

        var cut = RenderList(list.Id);

        Assert.Equal("Einkaufsliste", cut.Find("h1").TextContent);
        Assert.Equal(["Obst & Gemüse", "Kühlregal"], Headings(cut));
        Assert.Equal(["Äpfel", "Milch", "Joghurt"], cut.FindAll(".fa-list-row-name").Select(n => n.TextContent.Trim()));
        Assert.Contains("get-order:", Stores.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ListWithStore_FollowsTheOrderOfTheStore()
    {
        var store = AddStore("Markt");
        Stores.Learned[store.Id] = ["dairy_chilled", "bakery", .. SectionKeys.All.Except(["dairy_chilled", "bakery"])];
        var list = AddList("Wochenende", storeId: store.Id);
        AddItem(list.Id, "Äpfel", "produce");
        AddItem(list.Id, "Milch", "dairy_chilled");
        AddItem(list.Id, "Brot", "bakery");

        var cut = RenderList(list.Id);

        Assert.Equal("Wochenende", cut.Find("h1").TextContent);
        Assert.Equal(["Kühlregal", "Brot & Backwaren", "Obst & Gemüse"], Headings(cut));
        Assert.Contains($"get-order:{store.Id}", Stores.Calls);
    }

    [Fact]
    public void ItemsShowQuantityNoteAndTheAutomaticLabel()
    {
        var list = AddList();
        AddItem(list.Id, "Milch", "dairy_chilled", quantity: new QuantityDto(2, "l"), note: "laktosefrei", origin: ShoppingItemOriginDto.MinimumStock);

        var cut = RenderList(list.Id);

        var secondary = cut.Find(".fa-list-row-secondary").TextContent;
        Assert.Contains("2 l", secondary, StringComparison.Ordinal);
        Assert.Contains("laktosefrei", secondary, StringComparison.Ordinal);
        Assert.Contains("automatisch", secondary, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyList_ShowsTheEmptyState()
    {
        var list = AddList();

        var cut = RenderList(list.Id);

        Assert.Contains("Deine Einkaufsliste ist leer", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownList_OffersTheWayBack()
    {
        AddList();

        var cut = RenderList(Guid.CreateVersion7());

        Assert.Contains("Liste nicht gefunden", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("/shopping", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void Enter_AddsTheTypedTextAsFreeTextItem()
    {
        var list = AddList();
        var cut = RenderList(list.Id);

        cut.Find("form input").Input("  Eier  ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Eier", cut.Find(".fa-list-row-name").TextContent, StringComparison.Ordinal));
        var added = Assert.Single(Shopping.Items);
        Assert.Equal("Eier", added.Text);
        Assert.Null(added.ProductId);
        Assert.Equal(list.Id, added.ListId);
        Assert.Equal(string.Empty, cut.Find("form input").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void Enter_WithoutText_AddsNothing()
    {
        var list = AddList();
        var cut = RenderList(list.Id);

        cut.Find("form").Submit();

        Assert.Empty(Shopping.Items);
        Assert.DoesNotContain("add-item", Shopping.Calls);
    }

    [Fact]
    public async Task ChoosingAProduct_AddsAProductItem()
    {
        var milk = new ProductDto(Guid.CreateVersion7(), "Vollmilch", null, "dairy", "l", null, null, null, [], false, 1);
        Catalog.Products.Add(milk);
        Shopping.ProductNames[milk.Id] = "Vollmilch";
        var list = AddList();
        var cut = RenderList(list.Id);

        cut.FindAll("button").First(b => b.TextContent.Contains("Produkt wählen", StringComparison.Ordinal)).Click();
        var input = cut.Find(".fa-quick-add-picker input");
        input.Focus();
        input.Input("voll");
        Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-list-item")));
        await Popovers.InvokeAsync(() => Popovers.FindAll(".mud-list-item")[0].Click());

        cut.WaitForAssertion(() => Assert.Single(Shopping.Items));
        Assert.Equal(milk.Id, Shopping.Items[0].ProductId);
        cut.WaitForAssertion(() => Assert.Contains("Vollmilch", cut.Find(".fa-list-row-name").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Checking_MovesTheItemIntoTheCollapsedCartGroup()
    {
        var list = AddList();
        AddItem(list.Id, "Milch", "dairy_chilled");
        AddItem(list.Id, "Äpfel", "produce");
        var cut = RenderList(list.Id);

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(["Obst & Gemüse", "Im Wagen (1)"], Headings(cut));
        Assert.Empty(cut.FindAll("button[aria-label='Milch zurücknehmen']"));
        var toggle = cut.Find("button.fa-cart-toggle");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));

        toggle.Click();

        Assert.Equal("true", cut.Find("button.fa-cart-toggle").GetAttribute("aria-expanded"));
        cut.Find("button[aria-label='Milch zurücknehmen']").Click();
        cut.WaitForAssertion(() => Assert.Equal(["Kühlregal", "Obst & Gemüse"], Headings(cut).Order().ToList()));
        Assert.DoesNotContain(Shopping.Items, i => i.Checked);
    }

    [Fact]
    public void CompleteButton_NeedsCheckedItems_AndLeadsToTheCompletePage()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        var cut = RenderList(list.Id);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Einkauf abschließen", StringComparison.Ordinal));

        cut.Find("button[aria-label='Milch abhaken']").Click();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Einkauf abschließen", StringComparison.Ordinal)));
        cut.FindAll("button").First(b => b.TextContent.Contains("Einkauf abschließen", StringComparison.Ordinal)).Click();

        Assert.EndsWith($"/shopping/{list.Id:D}/complete", CurrentUri, StringComparison.Ordinal);
    }

    [Fact]
    public void FailingCheck_ShowsTheErrorAndKeepsTheItemOpen()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        var cut = RenderList(list.Id);
        Shopping.FailChangeWith = FakeAccountClient.Error(409, "shopping.unknown_version");

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.Contains("nicht auf dem aktuellen Stand", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("button[aria-label='Milch abhaken']"));
    }

    [Fact]
    public void EditButton_OpensTheItemDialog()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        var cut = RenderList(list.Id);

        cut.Find("button[aria-label='Milch bearbeiten']").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Notiz", Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void ListToolbar_OffersAllListsOnlyWithSeveralLists()
    {
        var list = AddList();
        var single = RenderList(list.Id);
        Assert.DoesNotContain(single.FindAll("a"), a => a.TextContent.Contains("Alle Listen", StringComparison.Ordinal));

        AddList("Wochenende", isDefault: false);
        var several = RenderList(list.Id);

        Assert.Contains(several.FindAll("a"), a => a.TextContent.Contains("Alle Listen", StringComparison.Ordinal));
    }

    [Fact]
    public void English_UsesEnglishGroupAndCartTexts()
    {
        UseCulture("en");
        var list = AddList();
        AddItem(list.Id, "Milk", "dairy_chilled");
        AddItem(list.Id, "Bread", "bakery", isChecked: true);

        var cut = RenderList(list.Id);

        Assert.Equal("Shopping list", cut.Find("h1").TextContent);
        Assert.Contains("In the cart (1)", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("button[aria-label='Check off Milk']"));
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Finish shopping", StringComparison.Ordinal));
    }
}

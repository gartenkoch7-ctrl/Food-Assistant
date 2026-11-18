using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Shopping;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingListPageRestrictedTests() : ShoppingTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_CanCheckAndAdd_ButNotCompleteOrEditTheList()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        AddItem(list.Id, "Brot", isChecked: true);

        var cut = Render<ShoppingListPage>(p => p.Add(c => c.ListId, list.Id));

        Assert.NotNull(cut.Find("button[aria-label='Milch abhaken']"));
        Assert.NotNull(cut.Find("form"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Einkauf abschließen", StringComparison.Ordinal));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("bearbeiten", StringComparison.Ordinal));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Liste anlegen", StringComparison.Ordinal));
    }

    [Fact]
    public void RestrictedMember_ChecksAnItem()
    {
        var list = AddList();
        AddItem(list.Id, "Milch");
        var cut = Render<ShoppingListPage>(p => p.Add(c => c.ListId, list.Id));

        cut.Find("button[aria-label='Milch abhaken']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal));
    }
}

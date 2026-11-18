using Bunit;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shopping;

public sealed class ShoppingListPageMergeTests : ShoppingTestContext
{
    private (IRenderedComponent<ShoppingListPage> Cut, ShoppingItemDto First, ShoppingItemDto Second) Duplicates()
    {
        var product = Guid.CreateVersion7();
        var list = AddList();
        var first = AddItem(list.Id, "Milch", "dairy_chilled", productId: product, quantity: new QuantityDto(1, "l"));
        var second = AddItem(list.Id, "Milch", "dairy_chilled", productId: product, quantity: new QuantityDto(2, "l"));
        return (Render<ShoppingListPage>(p => p.Add(c => c.ListId, list.Id)), first, second);
    }

    [Fact]
    public void TwoOpenItemsOfTheSameProduct_OfferMerging_WhichSumsTheQuantities()
    {
        var (cut, first, second) = Duplicates();

        Assert.Contains("steht mehrfach auf der Liste", cut.Find(".fa-duplicate-hint").TextContent, StringComparison.Ordinal);
        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".fa-duplicate-hint")));
        var change = Assert.Single(Shopping.Changes);
        Assert.Equal(new QuantityDto(3, "l"), change.Quantity);
        Assert.Equal(first.Version, change.BaseVersion);
        Assert.Contains($"delete-item:{second.Id}:{second.Version}", Shopping.Calls);
        Assert.Equal(first.Id, Assert.Single(Shopping.Items).Id);
        Assert.DoesNotContain("nichts zusammengeführt", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentUnits_AreReportedButNotMergeable()
    {
        var list = AddList();
        var product = Guid.CreateVersion7();
        AddItem(list.Id, "Milch", productId: product, quantity: new QuantityDto(1, "l"));
        AddItem(list.Id, "Milch", productId: product, quantity: new QuantityDto(500, "ml"));

        var cut = Render<ShoppingListPage>(p => p.Add(c => c.ListId, list.Id));

        Assert.NotNull(cut.Find(".fa-duplicate-hint"));
        Assert.Empty(cut.FindAll("button[aria-label='Milch zusammenführen']"));
    }

    [Fact]
    public void LostChange_MergesNothing_AndSaysSo()
    {
        var (cut, _, _) = Duplicates();
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Es wurde nichts zusammengeführt", cut.Markup, StringComparison.Ordinal));
        // The server kept the first item checked: it left the open items, but the notice stays.
        cut.WaitForAssertion(() => Assert.Contains("Im Wagen (1)", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Es wurde nichts zusammengeführt", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, Shopping.Items.Count);
        Assert.DoesNotContain(Shopping.Calls, c => c.StartsWith("delete-item", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ShoppingConflictOutcomeDto.LastWriteWins)]
    [InlineData(ShoppingConflictOutcomeDto.ChangeWinsOverDelete)]
    [InlineData(ShoppingConflictOutcomeDto.CheckedWinsOverDelete)]
    public void SumThatTheServerApplied_IsFollowedByTheDelete(ShoppingConflictOutcomeDto outcome)
    {
        var (cut, first, second) = Duplicates();
        Shopping.ChangeOutcome = outcome;

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        // The incoming state won, so stopping before the delete would leave the sum next to the duplicate.
        cut.WaitForAssertion(() => Assert.Contains($"delete-item:{second.Id}:{second.Version}", Shopping.Calls));
        Assert.Equal(first.Id, Assert.Single(Shopping.Items).Id);
        Assert.Equal(new QuantityDto(3, "l"), Shopping.Items[0].Quantity);
    }

    [Fact]
    public void AfterAMerge_TheMergeIsAnnounced_AndTheFocusMovesToTheQuickAddField()
    {
        var (cut, _, _) = Duplicates();
        var focusBefore = JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Milch zusammengeführt", Services.GetRequiredService<ILiveAnnouncer>().Message.Trim()));
        cut.WaitForAssertion(() => Assert.True(JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) > focusBefore));
    }

    [Fact]
    public void AfterAConflict_TheHintIsGoneToo_SoTheFocusMovesOnToo()
    {
        var (cut, _, _) = Duplicates();
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;
        var focusBefore = JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("nichts zusammengeführt", cut.Markup, StringComparison.Ordinal));
        Assert.True(JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) > focusBefore);
    }

    [Fact]
    public void LostDelete_AfterTheSum_ExplainsThatTheDuplicateRemains()
    {
        var (cut, _, _) = Duplicates();
        Shopping.DeleteOutcome = ShoppingConflictOutcomeDto.ChangeWinsOverDelete;

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("doppelte Eintrag konnte nicht entfernt werden", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(2, Shopping.Items.Count);
    }

    [Fact]
    public void FailingDelete_AfterTheSum_ExplainsThatTheDuplicateRemains()
    {
        var (cut, _, _) = Duplicates();
        Shopping.FailDeleteWith = FakeAccountClient.Error(500, "common.internal_error");

        cut.Find("button[aria-label='Milch zusammenführen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("doppelte Eintrag konnte nicht entfernt werden", cut.Markup, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.Equal(new QuantityDto(3, "l"), Shopping.Items[0].Quantity);
    }

    [Fact]
    public void English_ShowsTheEnglishConflictText()
    {
        UseCulture("en");
        var (cut, _, _) = Duplicates();
        Shopping.ChangeOutcome = ShoppingConflictOutcomeDto.CheckedWins;

        cut.Find("button[aria-label='Merge Milch']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Nothing was merged", cut.Markup, StringComparison.Ordinal));
    }
}

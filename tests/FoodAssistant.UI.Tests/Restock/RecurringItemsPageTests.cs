using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.UI.Restock;
using FoodAssistant.UI.Tests.Support;

namespace FoodAssistant.UI.Tests.Restock;

public sealed class RecurringItemsPageTests : RestockTestContext
{
    [Fact]
    public void NoRules_ShowTheEmptyStateWithACreateButton()
    {
        AddList();

        var cut = Render<RecurringItemsPage>();

        Assert.Equal("Wiederkehrende Artikel", cut.Find("h1").TextContent);
        Assert.Contains("Noch keine wiederkehrenden Artikel", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Regel anlegen", StringComparison.Ordinal));
    }

    [Fact]
    public void Rules_AreListedWithScheduleListAndNextDue_AndAccessibleButtonNames()
    {
        var list = AddList();
        AddRule(list.Id, "Milch");
        AddRule(list.Id, "Käse", RecurrenceFrequencyDto.Monthly, null, 31);
        AddRule(list.Id, "Brot", RecurrenceFrequencyDto.Daily, null);

        var cut = Render<RecurringItemsPage>();

        Assert.Equal(3, cut.FindAll("li.fa-list-row").Count);
        var text = cut.Markup;
        Assert.Contains("Milch (2 l)", text, StringComparison.Ordinal);
        Assert.Contains("wöchentlich, Montag · Einkaufsliste · nächster Termin 24.11.2026", text, StringComparison.Ordinal);
        Assert.Contains("monatlich, am 31.", text, StringComparison.Ordinal);
        Assert.Contains("täglich", text, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("button[aria-label='Milch bearbeiten']"));
        Assert.NotNull(cut.Find("button[aria-label='Milch löschen']"));
    }

    [Fact]
    public void Create_AddsTheRule()
    {
        var list = AddList();
        var cut = Render<RecurringItemsPage>();

        cut.FindAll("button").Last(b => b.TextContent.Contains("Regel anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Neue Regel", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Field("Artikel").Change("Brot");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("li.fa-list-row")));
        var request = Assert.Single(Restock.Requests);
        Assert.Equal(("Brot", list.Id, RecurrenceFrequencyDto.Weekly, DayOfWeek.Monday), (request.Text, request.ListId, request.Frequency, request.DayOfWeek));
    }

    [Fact]
    public void Delete_AsksForConfirmation_ThenRemovesTheRule()
    {
        var list = AddList();
        AddRule(list.Id, "Milch");
        var cut = Render<RecurringItemsPage>();

        cut.Find("button[aria-label='Milch löschen']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel löschen?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Noch keine wiederkehrenden Artikel", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(Restock.Rules);
    }

    [Fact]
    public void FailedLoad_ShowsTheError_NotTheEmptyState()
    {
        Restock.FailWith = FakeAccountClient.Error(0, ClientErrorCodes.Network);

        var cut = Render<RecurringItemsPage>();

        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.DoesNotContain("Noch keine wiederkehrenden Artikel", cut.Markup, StringComparison.Ordinal);
    }

}

public sealed class RecurringItemsPageEnglishTests() : RestockTestContext("en")
{
    [Fact]
    public void English_UsesEnglishTexts()
    {
        AddRule(AddList().Id);

        var cut = Render<RecurringItemsPage>();

        Assert.Equal("Recurring items", cut.Find("h1").TextContent);
        Assert.Contains("weekly, Monday · Shopping list · next due 11/24/2026", cut.Markup, StringComparison.Ordinal);
    }
}

public sealed class RecurringItemsPageRestrictedTests() : RestockTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_ReadsButCannotChange()
    {
        AddRule(AddList().Id);

        var cut = Render<RecurringItemsPage>();

        Assert.Single(cut.FindAll("li.fa-list-row"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Regel anlegen", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("button[aria-label='Milch bearbeiten']"));
        Assert.Empty(cut.FindAll("button[aria-label='Milch löschen']"));
    }
}

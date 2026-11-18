using Bunit;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.UI.Restock;
using FoodAssistant.UI.Tests.Support;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Restock;

public sealed class RecurringRuleDialogTests : RestockTestContext
{
    [Fact]
    public async Task Edit_ShowsTheRule_AndSavesChangesWithItsETag()
    {
        var list = AddList();
        var rule = AddRule(list.Id, "Milch");
        var reference = await ShowAsync<RecurringRuleDialog>(p =>
        {
            p.Add(d => d.Rule, rule);
            p.Add(d => d.Lists, [list]);
        });
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel bearbeiten", Dialogs.Markup, StringComparison.Ordinal));

        Assert.Equal("Milch", Dialogs.Field("Artikel").GetAttribute("value"));
        Dialogs.Field("Artikel").Change("Hafermilch");
        ButtonByText(Dialogs, "Speichern").Click();

        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        var stored = Assert.Single(Restock.Rules);
        Assert.Equal(("Hafermilch", 5L), (stored.Text, stored.Version));
        Assert.Equal(DayOfWeek.Monday, Assert.Single(Restock.Requests).DayOfWeek);
    }

    [Fact]
    public async Task StaleVersion_IsRefreshedOnce_AndTheSaveRepeated()
    {
        var list = AddList();
        var rule = AddRule(list.Id, "Milch");
        Restock.Rules[0] = rule with { Version = 9 }; // the server moved on (e.g. the due date)
        var reference = await ShowAsync<RecurringRuleDialog>(p =>
        {
            p.Add(d => d.Rule, rule);
            p.Add(d => d.Lists, [list]);
        });
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Field("Artikel").Change("Hafermilch");

        ButtonByText(Dialogs, "Speichern").Click();

        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal(("Hafermilch", 10L), (Restock.Rules[0].Text, Restock.Rules[0].Version));
    }

    [Fact]
    public async Task RuleEditedByAnotherPerson_IsNotOverwritten()
    {
        var list = AddList();
        var rule = AddRule(list.Id, "Milch");
        Restock.Rules[0] = rule with { Version = 9, Text = "Hafermilch" };
        await ShowAsync<RecurringRuleDialog>(p =>
        {
            p.Add(d => d.Rule, rule);
            p.Add(d => d.Lists, [list]);
        });
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Field("Artikel").Change("Soja");

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.NotEmpty(Dialogs.FindAll("[role=alert]")));
        Assert.Equal(("Hafermilch", 9L), (Restock.Rules[0].Text, Restock.Rules[0].Version));
    }

    [Fact]
    public async Task EmptyText_ShowsTheFieldError_AndSendsNothing()
    {
        var list = AddList();
        await ShowAsync<RecurringRuleDialog>(p => p.Add(d => d.Lists, [list]));
        Dialogs.WaitForAssertion(() => Assert.Contains("Neue Regel", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("Pflichtfeld", Dialogs.ErrorText(), StringComparison.Ordinal);
        Assert.Empty(Restock.Calls);
    }

    [Fact]
    public async Task ProductRule_HasAReadOnlyName_ThatCanBeReleased()
    {
        var list = AddList();
        var rule = AddRule(list.Id, "Butter", productId: Guid.CreateVersion7());
        await ShowAsync<RecurringRuleDialog>(p =>
        {
            p.Add(d => d.Rule, rule);
            p.Add(d => d.Lists, [list]);
        });
        Dialogs.WaitForAssertion(() => Assert.Contains("Regel bearbeiten", Dialogs.Markup, StringComparison.Ordinal));

        Assert.NotNull(Dialogs.Field("Artikel").GetAttribute("readonly"));
        await Dialogs.InvokeAsync(() => ButtonByText(Dialogs, "Produkt entfernen").Click());

        Dialogs.WaitForAssertion(() => Assert.Null(Dialogs.Field("Artikel").GetAttribute("readonly")));
    }

    [Fact]
    public async Task ServerError_IsShownInTheDialog_AndItStaysOpen()
    {
        var list = AddList();
        await ShowAsync<RecurringRuleDialog>(p => p.Add(d => d.Lists, [list]));
        Dialogs.WaitForAssertion(() => Assert.Contains("Neue Regel", Dialogs.Markup, StringComparison.Ordinal));
        Restock.FailWith = FakeAccountClient.Error(404, RestockErrorCodes.RuleNotFound);
        Dialogs.Field("Artikel").Change("Brot");

        ButtonByText(Dialogs, "Speichern").Click();

        Dialogs.WaitForAssertion(() => Assert.Contains("Diese Regel gibt es nicht mehr.", Dialogs.ErrorText(), StringComparison.Ordinal));
    }
}

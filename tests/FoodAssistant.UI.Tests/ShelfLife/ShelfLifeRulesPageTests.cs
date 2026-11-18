using Bunit;
using FoodAssistant.Client.ShelfLife;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.UI.ShelfLife;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.ShelfLife;

public sealed class ShelfLifeRulesPageTests : UiTestContext
{
    private readonly FakeShelfLifeClient _client = new();

    public ShelfLifeRulesPageTests()
    {
        Services.AddSingleton<IShelfLifeClient>(_client);
        _client.Rules.Add(FakeShelfLifeClient.Row("dairy", StorageTypeDto.Chilled, new ShelfLifeDaysDto(3, null, null, 2), source: "bzfe"));
        _client.Rules.Add(FakeShelfLifeClient.Row("dairy", StorageTypeDto.Frozen));
        _client.Rules.Add(FakeShelfLifeClient.Row("fish", StorageTypeDto.Chilled, own: new ShelfLifeDaysDto(1, null, null, null)));
    }

    [Fact]
    public void Page_ExplainsTheSafetyRules_AndListsCategoriesCollapsed()
    {
        var cut = Render<ShelfLifeRulesPage>();

        Assert.Equal("Haltbarkeitsregeln", cut.Find("h1").TextContent);
        Assert.Contains("Die KI schlägt keine Haltbarkeiten vor.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Fehlt eine Regel, gibt es kein berechnetes Datum.", cut.Markup, StringComparison.Ordinal);
        var toggles = cut.FindAll("button.fa-shelf-life-toggle");
        Assert.Equal(2, toggles.Count);
        Assert.Contains("Milchprodukte", toggles[0].TextContent, StringComparison.Ordinal);
        Assert.All(toggles, t => Assert.Equal("false", t.GetAttribute("aria-expanded")));
        Assert.Contains("Eigen", toggles[1].TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("h3"));
    }

    [Fact]
    public void Expanding_ShowsValuesWithOriginAsText()
    {
        _client.Sources.Add(new ShelfLifeSourceDto("bzfe", "BZfE: Milch", "https://www.bzfe.de/", new DateOnly(2026, 11, 18)));
        var cut = Render<ShelfLifeRulesPage>();

        cut.FindAll("button.fa-shelf-life-toggle")[0].Click();

        Assert.Equal("true", cut.FindAll("button.fa-shelf-life-toggle")[0].GetAttribute("aria-expanded"));
        Assert.Equal(["Gekühlt", "Gefroren"], cut.FindAll("h3").Select(h => h.TextContent));
        var chilled = cut.FindAll(".fa-shelf-life-row")[0];
        var values = chilled.QuerySelectorAll("dd").Select(d => d.TextContent.Trim()).ToList();
        Assert.Equal(["3 Tage", "Standard"], chilled.QuerySelectorAll("dd")[0].Children.Select(s => s.TextContent));
        Assert.Contains("kein Wert", values[1], StringComparison.Ordinal);
        Assert.Contains("Quelle: BZfE: Milch", chilled.TextContent, StringComparison.Ordinal);
        Assert.Contains("abgerufen am 18.11.2026", cut.Find(".fa-shelf-life-sources").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutSources_SaysThatNoDefaultsExistYet()
    {
        var cut = Render<ShelfLifeRulesPage>();

        Assert.Contains("noch keine Standardwerte", cut.Find(".fa-shelf-life-sources").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Member_EditsACategoryInTheDialog()
    {
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<ShelfLifeRulesPage>();
        cut.FindAll("button.fa-shelf-life-toggle")[0].Click();

        cut.Find("button[aria-label='Bearbeiten: Milchprodukte, Gekühlt']").Click();
        dialogs.WaitForAssertion(() => Assert.Equal(4, dialogs.FindAll("input").Count));
        dialogs.FindAll("input")[0].Input("5");
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Eigen", cut.FindAll(".fa-shelf-life-row")[0].TextContent, StringComparison.Ordinal));
        Assert.Contains("set dairy/Chilled", _client.Calls);
        Assert.Contains("5 Tage", cut.FindAll(".fa-shelf-life-row")[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Member_ResetsAnOverride()
    {
        var cut = Render<ShelfLifeRulesPage>();
        cut.FindAll("button.fa-shelf-life-toggle")[1].Click();

        cut.Find("button[aria-label='Auf Standard zurücksetzen: Fisch & Meeresfrüchte, Gekühlt']").Click();

        Assert.Contains("reset fish/Chilled", _client.Calls);
        Assert.DoesNotContain("Eigen", cut.Find(".fa-shelf-life-row").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button[aria-label^='Auf Standard']"));
    }

    [Fact]
    public void FailedLoad_ShowsTheError()
    {
        _client.FailWith = FakeAccountClient.Error(0, "client.network");

        var cut = Render<ShelfLifeRulesPage>();

        Assert.NotNull(cut.Find(".fa-api-error"));
        Assert.Empty(cut.FindAll("button.fa-shelf-life-toggle"));
    }
}

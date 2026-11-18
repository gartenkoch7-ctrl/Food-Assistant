using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Shell;
using FoodAssistant.UI.Tests.Support;
using FoodAssistant.UI.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shell;

public sealed class MorePageTests : UiTestContext
{
    private static IReadOnlyList<string> LabelsOf(AngleSharp.Dom.IElement fieldset) =>
        [.. fieldset.QuerySelectorAll("label").Select(l => l.TextContent.Trim())];

    [Fact]
    public void Page_HasTitleAndTwoNamedGroups()
    {
        var cut = Render<MorePage>();

        Assert.Equal("Mehr", cut.Find("h1").TextContent);
        var legends = cut.FindAll("fieldset > legend").Select(l => l.TextContent).ToList();
        Assert.Equal(["Farbschema", "Sprache der App"], legends);
    }

    [Fact]
    public void ThemeGroup_OffersAllFourModes()
    {
        var cut = Render<MorePage>();

        var themeGroup = cut.FindAll("fieldset")[0];
        Assert.Equal(["Wie dein Gerät", "Hell", "Dunkel", "Hoher Kontrast"], LabelsOf(themeGroup));
    }

    [Fact]
    public void LanguageGroup_OffersGermanAndEnglish()
    {
        var cut = Render<MorePage>();

        var languageGroup = cut.FindAll("fieldset")[1];
        Assert.Equal(["Deutsch", "English"], LabelsOf(languageGroup));
    }

    [Fact]
    public void Selection_ShowsTheCurrentThemeAndLanguage()
    {
        var cut = Render<MorePage>();

        var radios = cut.FindAll("input[type=radio]");
        Assert.True(radios[0].HasAttribute("checked"), "System theme selected by default");
        Assert.True(radios[4].HasAttribute("checked"), "German selected under the German culture");
        Assert.Equal(2, radios.Count(r => r.HasAttribute("checked")));
    }

    [Fact]
    public void Selection_FollowsTheEnglishCulture()
    {
        UseCulture("en");

        var cut = Render<MorePage>();

        Assert.True(cut.FindAll("input[type=radio]")[5].HasAttribute("checked"));
    }

    [Fact]
    public void ChoosingDark_ChangesTheStateAndStoresIt()
    {
        var cut = Render<MorePage>();

        cut.FindAll("input[type=radio]")[2].Change(true);

        Assert.Equal(ThemeMode.Dark, Services.GetRequiredService<ThemeState>().Mode);
        cut.WaitForAssertion(() => Assert.Equal("dark", Browser.Storage[BrowserThemePreferenceStore.StorageKey]));
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("input[type=radio]")[2].HasAttribute("checked")));
    }

    [Fact]
    public void ChoosingALanguage_DoesNotReloadUntilItIsApplied()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/more");
        var cut = Render<MorePage>();
        Assert.True(cut.Find("button.fa-apply-language").HasAttribute("disabled"), "nothing to apply yet");

        cut.FindAll("input[type=radio]")[5].Change(true);

        // Arrow keys in a radio group change the selection one by one; a reload here would trap keyboard users.
        Assert.Empty(Cultures.Chosen);
        Assert.Single(navigation.History);
        Assert.False(cut.Find("button.fa-apply-language").HasAttribute("disabled"));
    }

    [Fact]
    public void ApplyingTheLanguage_SetsItAndReloadsThePage()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/more");
        var cut = Render<MorePage>();
        cut.FindAll("input[type=radio]")[5].Change(true);

        cut.Find("button.fa-apply-language").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["en"], Cultures.Chosen);
            Assert.True(navigation.History.First().Options.ForceLoad);
        });
    }

    [Fact]
    public async Task LateStoredTheme_UpdatesTheSelection()
    {
        Browser.Storage[BrowserThemePreferenceStore.StorageKey] = "high-contrast";
        var cut = Render<MorePage>();
        var state = Services.GetRequiredService<ThemeState>();

        await cut.InvokeAsync(() => state.InitializeAsync(Xunit.TestContext.Current.CancellationToken));

        cut.WaitForAssertion(() => Assert.True(cut.FindAll("input[type=radio]")[3].HasAttribute("checked")));
    }
}

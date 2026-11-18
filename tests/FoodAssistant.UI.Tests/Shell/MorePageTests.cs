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
    public void Page_LinksToTheHouseholdAndStockPages()
    {
        var cut = Render<MorePage>();

        // "Haushalt wechseln" in the account section points to the household page as well.
        var links = cut.FindAll("ul.fa-more-links a").Select(a => (Href: a.GetAttribute("href"), Text: a.TextContent.Trim())).ToList();
        Assert.Equal(
            [
                ("/settings/household", "Haushalt & Mitglieder"),
                ("/settings/locations", "Lagerorte"),
                ("/settings/stores", "Läden"),
                ("/settings/shelf-life", "Haltbarkeitsregeln"),
                ("/products", "Produkte"),
                ("/shopping/recurring", "Wiederkehrende Artikel"),
                ("/settings/household", "Haushalt wechseln"),
            ],
            links);
        var headings = cut.FindAll("h2").Select(h => h.TextContent).ToList();
        Assert.Equal(["Haushalt", "Vorrat & Einkauf", "Darstellung", "Sprache", "Konto"], headings);
    }

    [Fact]
    public void AccountSection_ShowsTheUser_AndSignsOut()
    {
        var cut = Render<MorePage>();

        Assert.Contains("Angemeldet als Anna", cut.Find("#more-account + p").TextContent);
        Assert.Contains("Haushalt wechseln", cut.Markup, StringComparison.Ordinal);
        cut.Find("button.fa-sign-out").Click();

        cut.WaitForAssertion(() => Assert.Contains("logout", Accounts.Calls));
        Assert.Equal(FoodAssistant.UI.Session.SessionStatus.SignedOut, Session.Status);
    }

    [Fact]
    public void FailedSignOut_ShowsTheError_AndStaysSignedIn()
    {
        Accounts.LogoutError = FakeAccountClient.Error(0, FoodAssistant.Client.Http.ClientErrorCodes.Network);
        var cut = Render<MorePage>();

        cut.Find("button.fa-sign-out").Click();

        cut.WaitForAssertion(() => Assert.Contains("Server ist nicht erreichbar", cut.Find("[role=alert]").TextContent));
        Assert.Equal(FoodAssistant.UI.Session.SessionStatus.Ready, Session.Status);
        Assert.Contains("Angemeldet als Anna", cut.Find("#more-account + p").TextContent);
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

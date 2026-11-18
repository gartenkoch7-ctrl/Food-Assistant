using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.UI.Shell;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shell;

public sealed class MainLayoutTests : UiTestContext
{
    private IRenderedComponent<MainLayout> RenderLayout(string body = "<h1>Seite</h1>") =>
        Render<MainLayout>(p => p.Add(
            c => c.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, body))));

    [Fact]
    public void Layout_HasTheLandmarks()
    {
        var cut = RenderLayout();

        Assert.Single(cut.FindAll("header"));
        Assert.Single(cut.FindAll("nav"));
        var main = Assert.Single(cut.FindAll("main"));
        Assert.Equal("main-content", main.Id);
        Assert.Equal("-1", main.GetAttribute("tabindex"));
        Assert.NotNull(main.QuerySelector("h1"));
    }

    [Fact]
    public void SkipLink_IsTheFirstLink_AndPointsToTheMainContent()
    {
        var cut = RenderLayout();

        var first = cut.FindAll("a")[0];
        Assert.Contains("fa-skip-link", first.ClassList);
        Assert.Equal("Zum Inhalt", first.TextContent);
        Assert.EndsWith("#main-content", first.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void SkipLink_Click_MovesFocusToTheMainContent()
    {
        var cut = RenderLayout();

        cut.Find("a.fa-skip-link").Click();

        Assert.Equal(["main-content"], Browser.FocusedElements);
    }

    [Fact]
    public void SkipLink_KeepsThePageAddress_SoItWorksWithABaseHref()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/shopping");

        var cut = RenderLayout();

        Assert.EndsWith("/shopping#main-content", cut.Find("a.fa-skip-link").GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void SkipLink_FollowsNavigation()
    {
        var cut = RenderLayout();

        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/more");

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/more#main-content", cut.Find("a.fa-skip-link").GetAttribute("href"), StringComparison.Ordinal));
    }

    [Fact]
    public void Layout_HasADismissibleErrorBar_WithTranslatedText()
    {
        var cut = RenderLayout();

        var bar = cut.Find("#blazor-error-ui");
        Assert.Equal("alert", bar.GetAttribute("role"));
        Assert.Contains("Es ist ein Fehler aufgetreten.", bar.TextContent);
        Assert.Equal("Meldung schließen", bar.QuerySelector("button.dismiss")?.GetAttribute("aria-label"));
    }

    [Fact]
    public void Layout_HasOneLiveRegion_OutsideTheMainContent()
    {
        var cut = RenderLayout();

        var region = Assert.Single(cut.FindAll("[role=status][aria-live=polite]"));
        Assert.Null(region.Closest("main"));
    }

    [Fact]
    public void Layout_SendsAnonymousVisitorsToTheLoginPage()
    {
        Accounts.Me = null;
        ReloadSession();

        RenderLayout();

        Assert.Contains("/login?returnUrl=", Services.GetRequiredService<BunitNavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_ShowsTheLiveStatusInTheHeader_OnlyWhileTheFeedIsDown()
    {
        var cut = RenderLayout();
        Assert.Empty(cut.FindAll("header .fa-live-status"));

        Live.SetState(FoodAssistant.Client.Sync.LiveConnectionState.Reconnecting);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("header .fa-live-status")));
    }

    [Fact]
    public void Layout_ShowsTheWordmark()
    {
        var cut = RenderLayout();

        Assert.Equal("Food-Assistant", cut.Find("header .fa-wordmark").TextContent);
    }
}

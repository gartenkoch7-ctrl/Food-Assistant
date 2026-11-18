using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.UI.Shell;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Shell;

public sealed class AppNavTests : UiTestContext
{
    private sealed class CookingEnabled : IAppFeatures
    {
        public bool CookingAvailable => true;
    }

    private IRenderedComponent<AppNav> RenderNavAt(string path)
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);
        return Render<AppNav>();
    }

    private static string? CurrentLabel(IRenderedComponent<AppNav> cut) =>
        cut.FindAll("a[aria-current='page']").SingleOrDefault()?.QuerySelector(".fa-nav-label")?.TextContent;

    [Fact]
    public void Nav_IsALandmarkWithAName()
    {
        var cut = Render<AppNav>();

        var nav = Assert.Single(cut.FindAll("nav"));
        Assert.Equal("Hauptnavigation", nav.GetAttribute("aria-label"));
    }

    [Fact]
    public void Entries_AreInOrder_WithoutCooking()
    {
        var cut = Render<AppNav>();

        var labels = cut.FindAll(".fa-nav-label").Select(e => e.TextContent).ToList();
        Assert.Equal(["Vorrat", "Einkauf", "Scannen", "Mehr"], labels);
    }

    [Fact]
    public void Cooking_IsHidden_ByDefault()
    {
        var cut = Render<AppNav>();

        Assert.DoesNotContain("Kochen", cut.Markup);
        Assert.Empty(cut.FindAll("a[href='/cooking']"));
    }

    [Fact]
    public void Cooking_AppearsBetweenScanAndMore_WhenAvailable()
    {
        Services.AddSingleton<IAppFeatures, CookingEnabled>();

        var cut = Render<AppNav>();

        var labels = cut.FindAll(".fa-nav-label").Select(e => e.TextContent).ToList();
        Assert.Equal(["Vorrat", "Einkauf", "Scannen", "Kochen", "Mehr"], labels);
    }

    [Theory]
    [InlineData("/", "Vorrat")]
    [InlineData("/shopping", "Einkauf")]
    [InlineData("/scan", "Scannen")]
    [InlineData("/more", "Mehr")]
    [InlineData("/more?tab=1", "Mehr")]
    public void ActiveEntry_HasAriaCurrentPage(string path, string expectedLabel)
    {
        var cut = RenderNavAt(path);

        Assert.Equal(expectedLabel, CurrentLabel(cut));
    }

    [Fact]
    public void SubPage_MarksItsSectionAsCurrent_ButNotAsThePage()
    {
        var cut = RenderNavAt("/more/appearance");

        Assert.Null(CurrentLabel(cut));
        Assert.Equal("true", cut.Find("a[href='/more']").GetAttribute("aria-current"));
        Assert.Contains("fa-nav-item--active", cut.Find("a[href='/more']").ClassList);
    }

    [Fact]
    public void UnknownPage_HasNoActiveEntry()
    {
        var cut = RenderNavAt("/not-found");

        Assert.Empty(cut.FindAll("[aria-current]"));
    }

    [Fact]
    public void StartPage_IsNotActive_OnOtherPages()
    {
        var cut = RenderNavAt("/shopping");

        Assert.Null(cut.Find("a[href='/']").GetAttribute("aria-current"));
    }

    [Fact]
    public void ActiveEntry_FollowsNavigation()
    {
        var cut = RenderNavAt("/");

        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/shopping");

        cut.WaitForAssertion(() => Assert.Equal("Einkauf", CurrentLabel(cut)));
    }

    [Fact]
    public void ScanEntry_IsMarkedAsPrimaryAction()
    {
        var cut = Render<AppNav>();

        var primary = Assert.Single(cut.FindAll(".fa-nav-item--primary"));
        Assert.Equal("/scan", primary.GetAttribute("href"));
    }

    [Fact]
    public void Entries_AreLinksWithVisibleText_AndDecorativeIcons()
    {
        var cut = Render<AppNav>();

        foreach (var link in cut.FindAll("a.fa-nav-item"))
        {
            Assert.False(string.IsNullOrWhiteSpace(link.TextContent));
            Assert.Equal("true", link.QuerySelector("svg")?.GetAttribute("aria-hidden"));
        }
    }

    [Fact]
    public void Labels_FollowTheCulture()
    {
        UseCulture("en");

        var cut = Render<AppNav>();

        var labels = cut.FindAll(".fa-nav-label").Select(e => e.TextContent).ToList();
        Assert.Equal(["Pantry", "Shopping", "Scan", "More"], labels);
    }
}

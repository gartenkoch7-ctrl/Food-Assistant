using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Common;

public sealed class LiveRegionTests : UiTestContext
{
    [Fact]
    public void Region_IsPoliteStatus_AndHiddenVisually()
    {
        var cut = Render<LiveRegion>();

        var region = cut.Find("[role=status]");
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Contains("fa-visually-hidden", region.ClassList);
        Assert.Equal(string.Empty, region.TextContent);
    }

    [Fact]
    public void Announcing_PutsTheMessageIntoTheRegion()
    {
        var cut = Render<LiveRegion>();

        cut.InvokeAsync(() => Services.GetRequiredService<ILiveAnnouncer>().Announce("Milch wurde abgehakt"));

        cut.WaitForAssertion(() => Assert.Equal("Milch wurde abgehakt", cut.Find("[role=status]").TextContent));
    }

    [Fact]
    public void AnnouncingTheSameMessageTwice_ChangesTheText()
    {
        var announcer = Services.GetRequiredService<ILiveAnnouncer>();

        announcer.Announce("Hallo");
        var first = announcer.Message;
        announcer.Announce("Hallo");

        Assert.NotEqual(first, announcer.Message);
    }
}

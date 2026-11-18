using Bunit;
using FoodAssistant.Client.Sync;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Sync;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Sync;

public sealed class LiveStatusIndicatorTests : UiTestContext
{
    private ILiveAnnouncer Announcer => Services.GetRequiredService<ILiveAnnouncer>();

    [Fact]
    public void WhileConnected_NothingIsShown()
    {
        var cut = Render<LiveStatusIndicator>();

        Assert.Empty(cut.FindAll(".fa-live-status"));
    }

    [Theory]
    [InlineData(LiveConnectionState.Reconnecting)]
    [InlineData(LiveConnectionState.Disconnected)]
    public void WhenTheFeedIsDown_ASymbolAndTextAreShown(LiveConnectionState state)
    {
        var cut = Render<LiveStatusIndicator>();

        Live.SetState(state);

        cut.WaitForAssertion(() =>
        {
            var notice = cut.Find(".fa-live-status");
            Assert.Equal("status", notice.GetAttribute("role"));
            Assert.Equal("Verbindung unterbrochen – Daten können veraltet sein", notice.TextContent.Trim());
            Assert.NotNull(notice.QuerySelector("svg"));
        });
    }

    [Fact]
    public void WhenTheFeedIsDown_TheTextIsEnglishInEnglish()
    {
        UseCulture("en");
        var cut = Render<LiveStatusIndicator>();

        Live.SetState(LiveConnectionState.Disconnected);

        cut.WaitForAssertion(() =>
            Assert.Equal("Connection lost – data may be out of date", cut.Find(".fa-live-status").TextContent.Trim()));
    }

    [Fact]
    public void ALossIsAnnouncedOnce_AndSoIsTheRecovery()
    {
        var cut = Render<LiveStatusIndicator>();

        Live.SetState(LiveConnectionState.Reconnecting);
        cut.WaitForAssertion(() => Assert.StartsWith("Verbindung unterbrochen", Announcer.Message, StringComparison.Ordinal));
        var lost = Announcer.Message;
        Live.SetState(LiveConnectionState.Disconnected);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".fa-live-status")));
        Assert.Equal(lost, Announcer.Message);

        Live.SetState(LiveConnectionState.Connected);

        cut.WaitForAssertion(() => Assert.StartsWith("Verbindung wiederhergestellt", Announcer.Message, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll(".fa-live-status"));
    }

    [Fact]
    public void WithoutALiveFeed_NothingIsShownOrAnnounced()
    {
        var cut = Render<LiveStatusIndicator>();

        Live.SetState(LiveConnectionState.Off);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".fa-live-status")));
        Assert.Equal(string.Empty, Announcer.Message);
    }

    [Fact]
    public void AfterSignOut_ThePageIsNotMarkedAsStale()
    {
        var cut = Render<LiveStatusIndicator>();

        SignOut();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".fa-live-status")));
        Assert.Equal(string.Empty, Announcer.Message);
    }
}

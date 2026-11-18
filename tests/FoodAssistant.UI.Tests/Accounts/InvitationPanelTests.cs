using Bunit;
using FoodAssistant.Client.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class InvitationPanelTests : UiTestContext
{
    public InvitationPanelTests()
        : base(role: HouseholdRole.Owner)
    {
        Households = new FakeHouseholdClient();
        Clipboard = new FakeClipboard();
        Services.AddSingleton<IHouseholdClient>(Households);
        Services.AddSingleton<IClipboard>(Clipboard);
    }

    private FakeHouseholdClient Households { get; }

    private FakeClipboard Clipboard { get; }

    private string Announced => Services.GetRequiredService<ILiveAnnouncer>().Message.Trim();

    private IRenderedComponent<InvitationPanel> RenderPanel() =>
        Render<InvitationPanel>(p => p.Add(c => c.HouseholdId, FakeAccountClient.TestHouseholdId));

    private static void Create(IRenderedComponent<InvitationPanel> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Einladung erstellen").Click();

    [Fact]
    public void Roles_AreChosenWithRadios_MemberFirst()
    {
        var cut = RenderPanel();

        var labels = cut.FindAll("label.fa-option").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(3, labels.Count);
        Assert.StartsWith("Mitglied", labels[0], StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("input[type=radio]"), r => r.HasAttribute("checked"));
        Assert.Contains("Es gibt noch keine Einladungen.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShowsLinkWithTheTokenInTheFragment_AndAQrCode()
    {
        var cut = RenderPanel();
        cut.FindAll("input[type=radio]")[1].Change(true);

        Create(cut);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Field("Einladungslink")));
        var link = cut.Field("Einladungslink").GetAttribute("value");
        Assert.Equal("http://localhost/invite#token=tok%2B%2Fen", link);
        Assert.Contains("invitations.create:Restricted", Households.Calls);
        Assert.Contains("<svg", cut.Find(".fa-qr").InnerHtml, StringComparison.Ordinal);
        Assert.Contains("Gültig bis", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Einladung erstellt.", Announced);
        Assert.Contains("Eingeschränkt, offen, gültig bis", cut.Find("li.fa-invitation").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void QrCode_HasATextAlternative()
    {
        var cut = RenderPanel();
        Create(cut);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".fa-qr")));
        var qr = cut.Find(".fa-qr");
        Assert.Equal("img", qr.GetAttribute("role"));
        Assert.Contains("QR-Code mit dem Einladungslink", qr.GetAttribute("aria-label"), StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_WritesTheLinkToTheClipboard_AndAnnouncesIt()
    {
        var cut = RenderPanel();
        Create(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Field("Einladungslink")));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Link kopieren").Click();

        cut.WaitForAssertion(() => Assert.Equal("Link kopiert.", Announced));
        Assert.Equal(cut.Field("Einladungslink").GetAttribute("value"), Assert.Single(Clipboard.Written));
    }

    [Fact]
    public void Copy_WhenTheBrowserRefuses_SaysToCopyByHand()
    {
        Clipboard.Works = false;
        var cut = RenderPanel();
        Create(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Field("Einladungslink")));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Link kopieren").Click();

        cut.WaitForAssertion(() => Assert.Contains("von Hand", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(Clipboard.Written);
    }

    [Fact]
    public void Revoke_MarksTheInvitation_AndRemovesTheLink()
    {
        var cut = RenderPanel();
        Create(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Field("Einladungslink")));

        cut.Find("button[aria-label^='Einladung widerrufen']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Einladung widerrufen.", Announced));
        Assert.Contains("widerrufen", cut.Find("li.fa-invitation").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".fa-qr"));
        Assert.Empty(cut.FindAll("button[aria-label^='Einladung widerrufen']"));
    }

    [Fact]
    public void OnlyPendingInvitations_CanBeRevoked()
    {
        Households.Invitations.Add(new InvitationResponse(Guid.CreateVersion7(), HouseholdRole.Member, InvitationStatus.Accepted, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Households.Invitations.Add(new InvitationResponse(Guid.CreateVersion7(), HouseholdRole.Member, InvitationStatus.Expired, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Households.Invitations.Add(new InvitationResponse(Guid.CreateVersion7(), HouseholdRole.Owner, InvitationStatus.Pending, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));

        var cut = RenderPanel();

        Assert.Equal(3, cut.FindAll("li.fa-invitation").Count);
        Assert.Single(cut.FindAll("button[aria-label^='Einladung widerrufen']"));
        Assert.Contains("angenommen", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("abgelaufen", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateFailure_ShowsTheError_AndNoLink()
    {
        Households.Fail["invitations.create"] = FakeAccountClient.Error(403, HouseholdErrorCodes.Forbidden);
        var cut = RenderPanel();

        Create(cut);

        cut.WaitForAssertion(() => Assert.Contains("fehlt dir die Berechtigung", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll(".fa-qr"));
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");

        var cut = RenderPanel();

        Assert.Equal("Invitations", cut.Find("h2").TextContent);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Create invitation");
    }
}

using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class InviteAcceptPageTests : UiTestContext
{
    private const string Token = "tok+/en=";

    private readonly BunitNavigationManager _navigation;

    public InviteAcceptPageTests()
        : base(signedIn: false)
    {
        _navigation = Services.GetRequiredService<BunitNavigationManager>();
        _navigation.NavigateTo("/invite#token=" + Uri.EscapeDataString(Token));
    }

    private static void FillNewAccount(IRenderedComponent<InviteAcceptPage> cut)
    {
        cut.Field("Anzeigename").Input(" Ben ");
        cut.Field("Benutzername").Input(" ben ");
        cut.Field("Passwort").Input("correct horse battery");
    }

    [Fact]
    public void SignedOut_OffersANewAccount_OrTheLoginPage()
    {
        var cut = Render<InviteAcceptPage>();

        Assert.Equal("Einladung annehmen", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Field("Anzeigename"));
        Assert.NotNull(cut.Field("Benutzername"));
        Assert.Equal("new-password", cut.Field("Passwort").GetAttribute("autocomplete"));
        Assert.Contains("Ich habe ein Konto – anmelden", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NewAccount_SendsTheTokenFromTheFragment_SignsIn_AndJoinsTheHousehold()
    {
        var joined = Guid.CreateVersion7();
        Accounts.AcceptedHouseholdId = joined;
        Accounts.MeAfterLogin = new MeResponse(
            Guid.CreateVersion7(), "ben", "Ben", false, false,
            [new MeHouseholdResponse(FakeAccountClient.TestHouseholdId, "Eigenes", "Europe/Berlin", HouseholdRole.Owner),
             new MeHouseholdResponse(joined, "Neues Zuhause", "Europe/Berlin", HouseholdRole.Member)]);
        var cut = Render<InviteAcceptPage>();

        FillNewAccount(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/", _navigation.Uri, StringComparison.Ordinal));
        var request = Assert.Single(Accounts.AcceptRequests);
        Assert.Equal((Token, "ben", "Ben"), (request.Token, request.UserName, request.DisplayName));
        Assert.Contains("login:ben", Accounts.Calls);
        Assert.Equal(joined, Session.ActiveHouseholdId);
    }

    [Fact]
    public void SignedIn_JoinsWithTheExistingAccount_WithoutTypingAnything()
    {
        Accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member);
        ReloadSession();
        var cut = Render<InviteAcceptPage>();

        Assert.Empty(cut.FindAll("input"));
        Assert.Contains("Du bist als Anna angemeldet", cut.Markup, StringComparison.Ordinal);
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/", _navigation.Uri, StringComparison.Ordinal));
        var request = Assert.Single(Accounts.AcceptRequests);
        Assert.Equal(Token, request.Token);
        Assert.Null(request.UserName);
        Assert.DoesNotContain("login:", string.Join(",", Accounts.Calls), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(404, InvitationErrorCodes.InvalidOrExpired, "ungültig, abgelaufen")]
    [InlineData(409, InvitationErrorCodes.AlreadyMember, "schon Mitglied")]
    [InlineData(409, AccountErrorCodes.UserNameTaken, "schon vergeben")]
    [InlineData(429, "common.rate_limited", "Zu viele Versuche")]
    public void Errors_AreExplained(int status, string code, string expected)
    {
        Accounts.AcceptError = FakeAccountClient.Error(status, code);
        var cut = Render<InviteAcceptPage>();

        FillNewAccount(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("login:", string.Join(",", Accounts.Calls), StringComparison.Ordinal);
    }

    [Fact]
    public void FieldErrors_ShowAtTheField()
    {
        Accounts.AcceptError = new ApiError(
            400, AccountErrorCodes.ValidationFailed, null, new Dictionary<string, string[]> { ["password"] = ["password_too_short"] }, null);
        var cut = Render<InviteAcceptPage>();

        FillNewAccount(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("mindestens 10 Zeichen", cut.ErrorText(), StringComparison.Ordinal));
    }

    [Fact]
    public void HaveAnAccount_GoesToTheLogin_AndComesBackWithoutTheFragment()
    {
        var cut = Render<InviteAcceptPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Ich habe ein Konto", StringComparison.Ordinal)).Click();

        Assert.Contains("/login?returnUrl=%2Finvite", _navigation.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(Token), _navigation.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("tok", _navigation.Uri, StringComparison.Ordinal);

        // Back on /invite after signing in: the fragment is gone, the in-memory draft still has the token.
        _navigation.NavigateTo("/invite");
        Accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member);
        ReloadSession();
        var again = Render<InviteAcceptPage>();
        again.Find("button").Click();

        again.WaitForAssertion(() => Assert.Equal(Token, Assert.Single(Accounts.AcceptRequests).Token));
    }

    [Fact]
    public void TokenIsNeverStoredInTheBrowser()
    {
        var cut = Render<InviteAcceptPage>();
        FillNewAccount(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(Accounts.AcceptRequests));
        Assert.DoesNotContain(Browser.Storage.Values, value => value.Contains("tok", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutAToken_ExplainsThatTheLinkIsIncomplete()
    {
        _navigation.NavigateTo("/invite");

        var cut = Render<InviteAcceptPage>();

        Assert.Equal("Einladungslink unvollständig", cut.Find("h2").TextContent);
        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(Accounts.AcceptRequests);
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");

        var cut = Render<InviteAcceptPage>();

        Assert.Equal("Accept invitation", cut.Find("h1").TextContent);
        Assert.Contains("I have an account – sign in", cut.Markup, StringComparison.Ordinal);
    }
}

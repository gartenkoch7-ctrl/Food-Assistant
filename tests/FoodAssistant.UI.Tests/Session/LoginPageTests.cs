using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Session;

public sealed class LoginPageTests : UiTestContext
{
    public LoginPageTests()
        : base(signedIn: false)
    {
    }

    private static IRenderedComponent<LoginPage> Fill(IRenderedComponent<LoginPage> cut, string user, string password)
    {
        var inputs = cut.FindAll("input");
        inputs[0].Input(user);
        inputs[1].Input(password);
        return cut;
    }

    [Fact]
    public void Page_HasHeadingLabelledFieldsAndASubmitButton()
    {
        var cut = Render<LoginPage>();

        Assert.Equal("Anmelden", cut.Find("h1").TextContent);
        var labels = cut.FindAll("label").Select(l => l.TextContent.Trim().TrimEnd('*').Trim()).ToList();
        Assert.Contains("Benutzername", labels);
        Assert.Contains("Passwort", labels);
        Assert.Equal("password", cut.FindAll("input")[1].GetAttribute("type"));
        Assert.Equal("submit", cut.Find("button").GetAttribute("type"));
        Assert.Equal("username", cut.FindAll("input")[0].GetAttribute("autocomplete"));
    }

    [Fact]
    public void Success_LoadsTheSession_AndGoesToTheStartPage()
    {
        Accounts.MeAfterLogin = FakeAccountClient.MeWith(HouseholdRole.Member);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/login");
        var cut = Render<LoginPage>();

        Fill(cut, " anna ", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(SessionStatus.Ready, Session.Status));
        Assert.Contains("login:anna", Accounts.Calls);
        Assert.EndsWith("/", navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void ReturnUrl_WithALocalPath_IsFollowed()
    {
        Accounts.MeAfterLogin = FakeAccountClient.MeWith(HouseholdRole.Member);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/login?returnUrl=%2Fshopping");
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/shopping", navigation.Uri, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("/\t/evil.example/x")]
    [InlineData("/\n/evil.example/x")]
    [InlineData("/\r/evil.example/x")]
    public void ReturnUrl_ThatIsNotLocal_IsIgnored(string returnUrl)
    {
        Accounts.MeAfterLogin = FakeAccountClient.MeWith(HouseholdRole.Member);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(SessionStatus.Ready, Session.Status));
        Assert.DoesNotContain("evil", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongPassword_ShowsTheErrorAndClearsThePassword()
    {
        Accounts.LoginError = FakeAccountClient.Error(401, AccountErrorCodes.InvalidCredentials);
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "wrong");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Benutzername oder Passwort stimmt nicht.", cut.Find("[role=alert]").TextContent));
        Assert.Equal(string.Empty, cut.FindAll("input")[1].GetAttribute("value") ?? string.Empty);
        Assert.Equal(SessionStatus.SignedOut, Session.Status);
    }

    [Fact]
    public void LockedOut_ShowsTheLockText()
    {
        Accounts.LoginError = FakeAccountClient.Error(429, AccountErrorCodes.LockedOut);
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("15 Minuten", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void PlainHttp_ExplainsThatHttpsIsNeeded()
    {
        Accounts.LoginError = FakeAccountClient.Error(400, AccountErrorCodes.HttpsRequired);
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("nur über HTTPS", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void NetworkError_UsesTheGenericText()
    {
        Accounts.LoginError = new(0, "client.network", null, new Dictionary<string, string[]>(), null);
        var cut = Render<LoginPage>();

        Fill(cut, "anna", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Server ist nicht erreichbar", cut.Find("[role=alert]").TextContent));
    }
}

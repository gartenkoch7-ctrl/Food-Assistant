using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Client.Http;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class SetupPageTests : UiTestContext
{
    public SetupPageTests()
        : base(signedIn: false)
    {
        Accounts.IsSetupComplete = false;
        Accounts.MeAfterLogin = FakeAccountClient.MeWith(HouseholdRole.Owner);
        ReloadSession();
    }

    private static IRenderedComponent<SetupPage> Fill(IRenderedComponent<SetupPage> cut, string password = "correct horse battery", string repeat = "correct horse battery")
    {
        cut.Field("Anzeigename").Input(" Anna ");
        cut.Field("Benutzername").Input(" anna ");
        cut.Field("Passwort").Input(password);
        cut.Field("Passwort wiederholen").Input(repeat);
        cut.Field("Name des Haushalts").Input(" Zuhause ");
        cut.Field("Zeitzone").Input("Europe/Vienna");
        return cut;
    }

    [Fact]
    public void Page_HasHeadingLabelledFieldsAndASubmitButton()
    {
        var cut = Render<SetupPage>();

        Assert.Equal("Haushalt einrichten", cut.Find("h1").TextContent);
        foreach (var label in new[] { "Anzeigename", "Benutzername", "Passwort", "Passwort wiederholen", "Name des Haushalts", "Zeitzone" })
        {
            Assert.NotNull(cut.Field(label));
        }

        Assert.Equal("password", cut.Field("Passwort").GetAttribute("type"));
        Assert.Equal("new-password", cut.Field("Passwort").GetAttribute("autocomplete"));
        Assert.Equal("submit", cut.Find("button").GetAttribute("type"));
        Assert.Contains("Mindestens 10 Zeichen", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeZone_IsPreselected_AndOffersAnIanaList()
    {
        var cut = Render<SetupPage>();

        var zone = cut.Field("Zeitzone");
        Assert.False(string.IsNullOrWhiteSpace(zone.GetAttribute("value")));
        Assert.StartsWith("fa-zones-", zone.GetAttribute("list"), StringComparison.Ordinal);
        Assert.NotNull(cut.Find("datalist#" + zone.GetAttribute("list")));
    }

    [Fact]
    public void Success_SendsTrimmedValues_SignsIn_AndGoesToTheStartPage()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/setup");
        var cut = Render<SetupPage>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(SessionStatus.Ready, Session.Status));
        var request = Assert.Single(Accounts.SetupRequests);
        Assert.Equal(("anna", "Anna", "Zuhause", "Europe/Vienna"), (request.UserName, request.DisplayName, request.HouseholdName, request.TimeZone));
        Assert.Contains("login:anna", Accounts.Calls);
        Assert.EndsWith("/", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentPasswords_StayOnThePage_WithAFieldError()
    {
        var cut = Render<SetupPage>();

        Fill(cut, repeat: "something else entirely");
        cut.Find("form").Submit();

        Assert.Empty(Accounts.SetupRequests);
        Assert.Contains("Die Passwörter stimmen nicht überein.", cut.ErrorText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ServerFieldErrors_ShowAtTheFieldAndAsAnAlert()
    {
        Accounts.SetupError = new ApiError(
            400,
            AccountErrorCodes.ValidationFailed,
            null,
            new Dictionary<string, string[]> { ["password"] = ["password_too_short"], ["timeZone"] = ["invalid_time_zone"] },
            null);
        var cut = Render<SetupPage>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Bitte prüfe die markierten Felder.", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        var errors = cut.ErrorText();
        Assert.Contains("mindestens 10 Zeichen", errors, StringComparison.Ordinal);
        Assert.Contains("Unbekannte Zeitzone", errors, StringComparison.Ordinal);
        Assert.Equal(SessionStatus.NeedsSetup, Session.Status);
    }

    [Fact]
    public void SetupAlreadyDone_SaysSo()
    {
        Accounts.SetupError = FakeAccountClient.Error(409, AccountErrorCodes.SetupAlreadyDone);
        var cut = Render<SetupPage>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("schon abgeschlossen", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void PlainHttp_ExplainsThatHttpsIsNeeded()
    {
        Accounts.SetupError = FakeAccountClient.Error(400, AccountErrorCodes.HttpsRequired);
        var cut = Render<SetupPage>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("nur über HTTPS", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void WhenAccountsExist_TheSetupPageLeadsToTheLoginPage()
    {
        Accounts.IsSetupComplete = true;
        ReloadSession();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/setup");

        Render<SetupPage>();

        Assert.EndsWith("/login", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");

        var cut = Render<SetupPage>();

        Assert.Equal("Set up your household", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Field("Repeat password"));
    }
}

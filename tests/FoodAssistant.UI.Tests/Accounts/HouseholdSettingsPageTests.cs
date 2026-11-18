using Bunit;
using FoodAssistant.Client.Households;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Client.Http;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class HouseholdSettingsPageTests : UiTestContext
{
    private const string LeadDaysLabel = "Vorlauf „läuft bald ab“ (Tage)";

    public HouseholdSettingsPageTests()
        : base(role: HouseholdRole.Owner)
    {
        Households = new FakeHouseholdClient();
        Services.AddSingleton<IHouseholdClient>(Households);
        Services.AddSingleton<IClipboard>(new FakeClipboard());
    }

    private FakeHouseholdClient Households { get; }

    private string Announced => Services.GetRequiredService<ILiveAnnouncer>().Message.Trim();

    private void SignInWith(HouseholdRole role, int households = 1)
    {
        Accounts.Me = FakeAccountClient.MeWith(role, households);
        ReloadSession();
    }

    [Fact]
    public void Owner_SeesAndSavesTheSettings_WithTheETagOfTheRead()
    {
        var cut = Render<HouseholdSettingsPage>();

        Assert.Equal("Haushalt", cut.Find("h1").TextContent);
        Assert.Equal("Zuhause 1", cut.Field("Name des Haushalts").GetAttribute("value"));
        Assert.Equal("Europe/Berlin", cut.Field("Zeitzone").GetAttribute("value"));
        Assert.Equal("2", cut.Field(LeadDaysLabel).GetAttribute("value"));

        cut.Field("Name des Haushalts").Input(" Flat ");
        cut.Field(LeadDaysLabel).Input("5");
        cut.Find("section[aria-labelledby^=fa-settings] form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Einstellungen gespeichert.", Announced));
        var update = Assert.Single(Households.Updates);
        Assert.Equal(("Flat", "Europe/Berlin", 5), (update.Name, update.TimeZone, update.ExpiryLeadDays));
        Assert.Equal("\"7\"", Households.LastIfMatch);
        Assert.Equal("Flat", Households.Settings.Name);
    }

    [Fact]
    public void ChangingTheName_RefreshesTheSessionNames()
    {
        var cut = Render<HouseholdSettingsPage>();
        var before = Accounts.Calls.Count(c => c == "me");

        cut.Field("Name des Haushalts").Input("Flat");
        cut.Find("section[aria-labelledby^=fa-settings] form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(before + 1, Accounts.Calls.Count(c => c == "me")));
    }

    [Fact]
    public void Member_OnlyReadsTheSettings()
    {
        SignInWith(HouseholdRole.Member);

        var cut = Render<HouseholdSettingsPage>();

        Assert.Empty(cut.FindAll("section[aria-labelledby^=fa-settings] input"));
        Assert.Contains("Nur Besitzer können die Einstellungen ändern.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Europe/Berlin", cut.Find("dl").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("section[aria-labelledby^=fa-invitations]"));
    }

    [Fact]
    public void Restricted_ReadsTheSettings_ButNeverAsksForMembers()
    {
        SignInWith(HouseholdRole.Restricted);

        var cut = Render<HouseholdSettingsPage>();

        Assert.Contains("Europe/Berlin", cut.Find("dl").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("members.list", Households.Calls);
        Assert.Empty(cut.FindAll("section[aria-labelledby^=fa-members]"));
    }

    [Fact]
    public void ChangedElsewhere_ShowsTheConflict_AndTheStoredValues()
    {
        var cut = Render<HouseholdSettingsPage>();
        Households.ChangeElsewhere("Von Ben umbenannt");

        cut.Field("Name des Haushalts").Input("Mein Name");
        cut.Find("section[aria-labelledby^=fa-settings] form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Inzwischen geändert", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Von Ben umbenannt", cut.Field("Name des Haushalts").GetAttribute("value"));
    }

    [Fact]
    public void ServerFieldError_ShowsAtTheField()
    {
        Households.Fail["settings.update"] = new ApiError(
            400, HouseholdErrorCodes.ValidationFailed, null, new Dictionary<string, string[]> { ["expiryLeadDays"] = ["out_of_range"] }, null);
        var cut = Render<HouseholdSettingsPage>();

        cut.Field(LeadDaysLabel).Input("20");
        cut.Find("section[aria-labelledby^=fa-settings] form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("außerhalb des erlaubten Bereichs", cut.ErrorText(), StringComparison.Ordinal));
    }

    [Fact]
    public void LoadFailure_ShowsTheErrorInsteadOfTheForm()
    {
        Households.Fail["settings.get"] = FakeAccountClient.Error(404, HouseholdErrorCodes.NotFound);

        var cut = Render<HouseholdSettingsPage>();

        Assert.Contains("nicht gefunden", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("section[aria-labelledby^=fa-settings] form"));
    }

    [Fact]
    public void OneHousehold_HasNoSwitcher()
    {
        var cut = Render<HouseholdSettingsPage>();

        Assert.Empty(cut.FindAll(".fa-switcher input[type=radio]"));
    }

    [Fact]
    public void Switcher_ListsTheHouseholdsWithRole_AndSwitchesOnlyOnTheButton()
    {
        SignInWith(HouseholdRole.Owner, households: 2);
        var second = Accounts.Me!.Households[1].Id;
        var cut = Render<HouseholdSettingsPage>();

        var radios = cut.FindAll(".fa-switcher input[type=radio]");
        Assert.Equal(2, radios.Count);
        Assert.Contains("Zuhause 2 – Besitzer", cut.Markup, StringComparison.Ordinal);
        var apply = cut.Find("button.fa-switcher-apply");
        Assert.True(apply.HasAttribute("disabled"));

        radios[1].Change(true);
        Assert.Equal(FakeAccountClient.TestHouseholdId, Session.ActiveHouseholdId);
        Assert.False(cut.Find("button.fa-switcher-apply").HasAttribute("disabled"));
        cut.Find("button.fa-switcher-apply").Click();

        cut.WaitForAssertion(() => Assert.Equal(second, Session.ActiveHouseholdId));
        Assert.Equal(second.ToString(), Browser.Storage[SessionState.HouseholdStorageKey]);
        Assert.Equal("Haushalt gewechselt zu Zuhause 2.", Announced);
    }

    [Fact]
    public void AfterSwitching_TheSectionsLoadTheNewHousehold()
    {
        SignInWith(HouseholdRole.Owner, households: 2);
        var second = Accounts.Me!.Households[1].Id;
        var cut = Render<HouseholdSettingsPage>();
        Households.ReadFor.Clear();

        cut.FindAll(".fa-switcher input[type=radio]")[1].Change(true);
        cut.Find("button.fa-switcher-apply").Click();

        cut.WaitForAssertion(() => Assert.Contains(second, Households.ReadFor));
        Assert.DoesNotContain(FakeAccountClient.TestHouseholdId, Households.ReadFor);
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");

        var cut = Render<HouseholdSettingsPage>();

        Assert.Equal("Household", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Field("Household name"));
        Assert.NotNull(cut.Field("Time zone"));
    }
}

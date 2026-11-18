using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Catalog;

namespace FoodAssistant.UI.Tests.Catalog;

public sealed class StorageLocationsPageTests : CatalogTestContext
{
    [Fact]
    public void NoLocations_OffersToCreateTheStandardOnes()
    {
        var cut = Render<StorageLocationsPage>();

        Assert.Equal("Lagerorte", cut.Find("h1").TextContent);
        cut.FindAll("button").First(b => b.TextContent.Contains("Standard-Lagerorte anlegen", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("li.fa-list-row").Count));
        Assert.Equal(["Kühlschrank", "Gefrierschrank", "Vorratsschrank"], Catalog.Locations.Select(l => l.Name));
        Assert.Equal([StorageTypeDto.Chilled, StorageTypeDto.Frozen, StorageTypeDto.Ambient], Catalog.Locations.Select(l => l.Type));
    }

    [Fact]
    public void Standards_UseTheEnglishNamesInEnglish()
    {
        UseCulture("en");
        var cut = Render<StorageLocationsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Add standard storage locations", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(["Fridge", "Freezer", "Pantry"], Catalog.Locations.Select(l => l.Name)));
    }

    [Fact]
    public void Locations_ShowTheirTypeAsText_AndOfferEditAndDelete()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));
        Catalog.Locations.Add(Location("Truhe", StorageTypeDto.Frozen));

        var cut = Render<StorageLocationsPage>();

        var rows = cut.FindAll("li.fa-list-row");
        Assert.Contains("Vorratsschrank", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Tiefkühler", rows[1].TextContent, StringComparison.Ordinal);
        Assert.NotNull(rows[0].QuerySelector("button[aria-label='Keller bearbeiten']"));
        Assert.NotNull(rows[0].QuerySelector("button[aria-label='Keller löschen']"));
    }

    [Fact]
    public void Create_OpensTheDialog_AndAddsTheLocation()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));
        var cut = Render<StorageLocationsPage>();

        cut.FindAll("button").Last(b => b.TextContent.Contains("Lagerort anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Typ", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Speisekammer");
        Dialogs.FindAll("input[type=radio]")[2].Change(true);
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("li.fa-list-row").Count));
        Assert.Contains(Catalog.Locations, l => l is { Name: "Speisekammer", Type: StorageTypeDto.Ambient });
    }

    [Fact]
    public void Edit_RenamesTheLocation()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));
        var cut = Render<StorageLocationsPage>();

        cut.Find("button[aria-label='Keller bearbeiten']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort bearbeiten", Dialogs.Markup, StringComparison.Ordinal));
        InputFor(Dialogs, "Name").Change("Vorratskeller");
        ButtonByText(Dialogs, "Speichern").Click();

        cut.WaitForAssertion(() => Assert.Contains("Vorratskeller", cut.Find("li.fa-list-row").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Dialog_RefusesAnEmptyName_WithoutCallingTheServer()
    {
        var cut = Render<StorageLocationsPage>();
        cut.FindAll("button").Last(b => b.TextContent.Contains("Lagerort anlegen", StringComparison.Ordinal)).Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Typ", Dialogs.Markup, StringComparison.Ordinal));

        ButtonByText(Dialogs, "Speichern").Click();

        Assert.Contains("Bitte ausfüllen.", Dialogs.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("create-location", Catalog.Calls);
    }

    [Fact]
    public void Delete_AsksForConfirmation_ThenRemovesTheLocation()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));
        var cut = Render<StorageLocationsPage>();

        cut.Find("button[aria-label='Keller löschen']").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Lagerort löschen?", Dialogs.Markup, StringComparison.Ordinal));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Noch keine Lagerorte", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(Catalog.Locations);
    }

    [Fact]
    public void Delete_OfALocationInUse_ExplainsWhy()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));
        var cut = Render<StorageLocationsPage>();
        Catalog.FailWith = new ApiError(409, "catalog.location_in_use", null, new Dictionary<string, string[]>(), null);

        cut.Find("button[aria-label='Keller löschen']").Click();
        Dialogs.WaitForAssertion(() => Dialogs.Find(".mud-message-box__yes-button"));
        Dialogs.Find(".mud-message-box__yes-button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Chargen", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
    }
}

public sealed class StorageLocationsPageRestrictedTests() : CatalogTestContext(role: HouseholdRole.Restricted)
{
    [Fact]
    public void RestrictedMember_SeesTheLocations_ButNoButtons()
    {
        Catalog.Locations.Add(Location("Keller", StorageTypeDto.Ambient));

        var cut = Render<StorageLocationsPage>();

        Assert.Single(cut.FindAll("li.fa-list-row"));
        Assert.Empty(cut.FindAll("button"));
    }
}

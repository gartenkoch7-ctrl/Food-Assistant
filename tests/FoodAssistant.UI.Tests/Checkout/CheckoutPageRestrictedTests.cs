using Bunit;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.UI.Tests.Checkout;

public sealed class CheckoutPageRestrictedTests() : CheckoutTestContext(HouseholdRole.Restricted)
{
    [Fact]
    public void Restricted_SeesAHint_AndNoProposalIsLoaded()
    {
        AddEntry("Milch", Guid.CreateVersion7(), Fridge.Id);

        var cut = RenderPage();

        Assert.Contains("Nur Mitglieder können einen Einkauf in den Vorrat buchen.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Contains("Zurück zur Liste", StringComparison.Ordinal) && a.GetAttribute("href") == $"/shopping/{ListId:D}");
        Assert.Empty(cut.FindAll("li.fa-checkout-row"));
        Assert.Equal(0, CheckoutClient.ProposalCalls);
    }
}

using Bunit;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Session;

public sealed class SessionGateTests : UiTestContext
{
    private IRenderedComponent<SessionGate> RenderGate() =>
        Render<SessionGate>(p => p.Add(c => c.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<p class=\"body\">Inhalt</p>"))));

    [Fact]
    public void Ready_ShowsTheBody()
    {
        var cut = RenderGate();

        Assert.Equal("Inhalt", cut.Find("p.body").TextContent);
    }

    [Fact]
    public void NoHousehold_ShowsTheHint_NotTheBody()
    {
        Accounts.Me = FakeAccountClient.MeWith(HouseholdRole.Member, households: 0);
        ReloadSession();

        var cut = RenderGate();

        Assert.Empty(cut.FindAll("p.body"));
        Assert.Equal("Du gehörst noch zu keinem Haushalt", cut.Find("h2").TextContent);
        Assert.Contains("Abmelden", cut.Find("button").TextContent);
    }

    [Fact]
    public void SigningOut_LeadsToTheLoginPage()
    {
        var navigation = Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>();
        navigation.NavigateTo("/more");
        var cut = RenderGate();

        SignOut();

        cut.WaitForAssertion(() => Assert.Contains("/login?returnUrl=%2Fmore", navigation.Uri, StringComparison.Ordinal));
    }
}

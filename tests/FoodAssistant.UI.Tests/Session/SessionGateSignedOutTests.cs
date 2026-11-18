using Bunit;
using Bunit.TestDoubles;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Session;

public sealed class SessionGateSignedOutTests : UiTestContext
{
    public SessionGateSignedOutTests()
        : base(signedIn: false)
    {
    }

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<SessionGate> RenderGate() =>
        Render<SessionGate>(p => p.Add(c => c.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<p class=\"body\">Inhalt</p>"))));

    [Fact]
    public void SignedOut_RedirectsToLogin_WithALocalReturnUrl()
    {
        Navigation.NavigateTo("/shopping?x=1");

        RenderGate();

        Assert.EndsWith("/login?returnUrl=%2Fshopping%3Fx%3D1", Navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsSetup_RedirectsToSetup()
    {
        Accounts.IsSetupComplete = false;
        ReloadSession();
        Navigation.NavigateTo("/");

        RenderGate();

        Assert.EndsWith("/setup", Navigation.Uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/setup")]
    [InlineData("/invite/abc")]
    public void PublicPaths_AreAlwaysShown(string path)
    {
        Navigation.NavigateTo(path);

        var cut = RenderGate();

        Assert.Equal("Inhalt", cut.Find("p.body").TextContent);
        Assert.EndsWith(path, Navigation.Uri, StringComparison.Ordinal);
    }
}

using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Tests.Common;

public sealed class ApiErrorAlertTests : UiTestContext
{
    private IRenderedComponent<ApiErrorAlert> RenderAlert(ApiError? error, bool withFeatureText = false) =>
        Render<ApiErrorAlert>(p =>
        {
            p.Add(c => c.Error, error);
            if (withFeatureText)
            {
                p.Add(c => c.FeatureText, Services.GetRequiredService<IStringLocalizer<SessionText>>());
            }
        });

    [Fact]
    public void NoError_RendersNothing() => Assert.Empty(RenderAlert(null).FindAll("[role=alert]"));

    [Fact]
    public void FeatureText_WinsForItsOwnCodes()
    {
        var cut = RenderAlert(FakeAccountClient.Error(401, "accounts.invalid_credentials"), withFeatureText: true);

        Assert.Equal("Benutzername oder Passwort stimmt nicht.", cut.Find("[role=alert]").TextContent.Trim());
    }

    [Theory]
    [InlineData(0, "client.network", "Der Server ist nicht erreichbar")]
    [InlineData(0, "client.offline", "Du bist offline")]
    [InlineData(412, "common.precondition_failed", "Inzwischen geändert – neu geladen")]
    [InlineData(500, "common.internal_error", "Das hat nicht geklappt")]
    public void UnknownCodes_FallBackToTheCommonTexts(int status, string code, string expected)
    {
        var cut = RenderAlert(FakeAccountClient.Error(status, code), withFeatureText: true);

        Assert.Contains(expected, cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void English_IsUsedUnderTheEnglishCulture()
    {
        UseCulture("en");

        var cut = RenderAlert(FakeAccountClient.Error(0, "client.offline"));

        Assert.Contains("You are offline", cut.Find("[role=alert]").TextContent);
    }
}

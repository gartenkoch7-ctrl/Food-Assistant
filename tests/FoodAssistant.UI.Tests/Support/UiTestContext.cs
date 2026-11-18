using System.Globalization;
using Bunit;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// bUnit context with the real UI registrations, a fake browser and German texts. JS calls MudBlazor
/// makes on its own (popovers, theme watching) are answered loosely.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext(string culture = "de")
    {
        UseCulture(culture);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Browser = new FakeBrowserInterop();

        // Registered before the UI services so the real, JS-based implementation does not win.
        Services.AddSingleton<IBrowserInterop>(Browser);
        Cultures = new FakeCultureService();
        Services.AddSingleton<ICultureService>(Cultures);
        Services.AddFoodAssistantUi();
    }

    protected FakeBrowserInterop Browser { get; }

    protected FakeCultureService Cultures { get; }

    /// <summary>Texts come from <c>CurrentUICulture</c>; setting it per test keeps tests independent of the machine.</summary>
    protected static void UseCulture(string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
    }
}

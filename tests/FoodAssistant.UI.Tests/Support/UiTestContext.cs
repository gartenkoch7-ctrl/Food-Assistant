using System.Globalization;
using Bunit;
using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Catalog;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Composition;
using FoodAssistant.UI.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// bUnit context with the real UI registrations, a fake browser, fake API clients and German texts.
/// JS calls MudBlazor makes on its own (popovers, theme watching) are answered loosely. By default a
/// member of one test household is signed in, so pages render behind the session gate.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext(string culture = "de", bool signedIn = true, HouseholdRole role = HouseholdRole.Member)
    {
        UseCulture(culture);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Browser = new FakeBrowserInterop();

        // Registered before the UI services so the real, JS-based implementation does not win.
        Services.AddSingleton<IBrowserInterop>(Browser);
        Cultures = new FakeCultureService();
        Services.AddSingleton<ICultureService>(Cultures);
        Accounts = new FakeAccountClient { Me = signedIn ? FakeAccountClient.MeWith(role) : null };
        Catalog = new FakeCatalogClient();
        Live = new FakeLiveUpdates();
        Services.AddSingleton<IAccountClient>(Accounts);
        Services.AddSingleton<ICatalogClient>(Catalog);
        Services.AddSingleton<ILiveUpdates>(Live);

        // Built by hand (not resolved) so tests can still register more services before the first render.
        // The fakes answer synchronously, so loading does not block.
        Expiry = new SessionExpiry();
        Services.AddSingleton(Expiry);
        Session = new SessionState(Accounts, Browser, Live, Expiry, TimeProvider.System, NullLogger<SessionState>.Instance);
        Session.LoadAsync().GetAwaiter().GetResult();
        Services.AddSingleton(Session);
        Services.AddFoodAssistantUi();
    }

    protected FakeBrowserInterop Browser { get; }

    protected FakeCultureService Cultures { get; }

    protected FakeAccountClient Accounts { get; }

    protected FakeCatalogClient Catalog { get; }

    protected FakeLiveUpdates Live { get; }

    protected SessionState Session { get; }

    protected SessionExpiry Expiry { get; }

    /// <summary>Asks the fake account client again (after a test changed <see cref="Accounts"/>); the fake answers synchronously.</summary>
    protected void ReloadSession() => Session.LoadAsync().GetAwaiter().GetResult();

    /// <summary>Signs out through the session, as the sign-out buttons do; the fake answers synchronously.</summary>
    protected void SignOut() => Session.SignOutAsync().GetAwaiter().GetResult();

    /// <summary>Texts come from <c>CurrentUICulture</c>; setting it per test keeps tests independent of the machine.</summary>
    protected static void UseCulture(string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
    }
}

using FoodAssistant.Client.Scan;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Scan;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Session;
using FoodAssistant.UI.Shell;
using FoodAssistant.UI.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace FoodAssistant.UI.Composition;

/// <summary>Service registration shared by every host of the UI: the server (prerendering), the WebAssembly client and later the MAUI app.</summary>
public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registers MudBlazor, localization, theme and browser services. Hosts may register their own
    /// <see cref="IAppFeatures"/> or stores before calling this; existing registrations win.
    /// </summary>
    public static IServiceCollection AddFoodAssistantUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMudServices();
        services.AddLocalization();

        // Scoped: on the server the JS runtime belongs to one circuit or prerender request.
        services.TryAddScoped<IBrowserInterop, BrowserInterop>();
        services.TryAddScoped<IThemePreferenceStore, BrowserThemePreferenceStore>();
        services.TryAddScoped<ThemeState>();
        services.TryAddScoped<ICultureService, CultureService>();
        services.TryAddSingleton<IAppFeatures, DefaultAppFeatures>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<SessionState>();
        services.TryAddScoped<ILiveAnnouncer, LiveAnnouncer>();
        services.TryAddScoped<InvitationDraft>();
        services.TryAddScoped<IClipboard, BrowserClipboard>();
        services.TryAddScoped<IUndoNotifier, UndoNotifier>();
        services.TryAddScoped<IBarcodeScanner, BrowserBarcodeScanner>();
        services.TryAddScoped<ScanFlow>();
        return services;
    }
}

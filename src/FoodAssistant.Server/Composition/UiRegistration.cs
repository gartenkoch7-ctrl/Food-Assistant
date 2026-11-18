using FoodAssistant.Client.Composition;
using FoodAssistant.Server.Components.Localization;
using FoodAssistant.UI.Composition;
using Microsoft.AspNetCore.Hosting;

namespace FoodAssistant.Server.Composition;

/// <summary>
/// Server-side services of the Blazor host. Pages run in WebAssembly without prerendering; the server
/// only renders the HTML shell (<c>App.razor</c>) and the error page, but the UI services must still
/// resolve for DI validation. Owned by the UI track.
/// </summary>
public static class UiRegistration
{
    public static WebApplicationBuilder AddUi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Pages are no longer rendered on the server, but the UI services must still resolve completely
        // (DI validation); the address is only a base for the client and never called from here.
        var publicUrl = Uri.TryCreate(builder.Configuration[TokenServerRegistration.PublicUrlKey], UriKind.Absolute, out var configured)
            ? configured
            : new Uri("http://localhost/");
        builder.Services.AddFoodAssistantClient(publicUrl);
        builder.Services.AddFoodAssistantUi();

        // The shell's html lang attribute and the server-rendered error page must speak the language the
        // WebAssembly client picks: the culture cookie written by the "More" page, else the browser's first language (ADR-0008).
        builder.Services.AddTransient<IStartupFilter, RequestLocalizationStartupFilter>();
        return builder;
    }
}

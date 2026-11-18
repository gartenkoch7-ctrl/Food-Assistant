using FoodAssistant.Client.Composition;
using FoodAssistant.Server.Components.Localization;
using FoodAssistant.UI.Composition;
using Microsoft.AspNetCore.Hosting;

namespace FoodAssistant.Server.Composition;

/// <summary>Server-side services the Blazor UI needs while prerendering (MudBlazor, localization). Owned by the UI track.</summary>
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

        // Prerendering must speak the language the WebAssembly client will pick afterwards (ADR-0008).
        builder.Services.AddTransient<IStartupFilter, RequestLocalizationStartupFilter>();
        return builder;
    }
}

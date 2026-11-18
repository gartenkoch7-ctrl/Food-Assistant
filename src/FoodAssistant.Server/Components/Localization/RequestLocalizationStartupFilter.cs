using FoodAssistant.UI.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace FoodAssistant.Server.Components.Localization;

/// <summary>
/// Adds request localization in front of the whole pipeline without touching <c>Program.cs</c>.
/// Order of the providers: query string, culture cookie (written by the "More" page), Accept-Language.
/// Unsupported languages get English (ADR-0008).
/// </summary>
internal sealed class RequestLocalizationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            string[] cultures = [.. SupportedCultures.All];
            app.UseRequestLocalization(options =>
            {
                options.SetDefaultCulture(SupportedCultures.Fallback)
                    .AddSupportedCultures(cultures)
                    .AddSupportedUICultures(cultures);

                // The WebAssembly client only looks at the browser's first language. The server must agree,
                // or "fr, de;q=0.8" would prerender in German and then flip to English.
                foreach (var provider in options.RequestCultureProviders.OfType<AcceptLanguageHeaderRequestCultureProvider>())
                {
                    provider.MaximumAcceptLanguageHeaderValuesToTry = 1;
                }
            });
            next(app);
        };
    }
}

namespace FoodAssistant.Server.Composition;

/// <summary>Server-side services the Blazor UI needs while prerendering (MudBlazor, localization). Owned by the UI track.</summary>
public static class UiRegistration
{
    public static WebApplicationBuilder AddUi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}

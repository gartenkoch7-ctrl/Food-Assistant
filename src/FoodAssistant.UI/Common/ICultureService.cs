namespace FoodAssistant.UI.Common;

/// <summary>The language of the UI: taken from the browser, overridable per device on the "More" page.</summary>
public interface ICultureService
{
    /// <summary>The language currently in effect (<see cref="SupportedCultures"/>).</summary>
    string Current { get; }

    /// <summary>The stored language, or the browser language if none was stored; always a supported language.</summary>
    Task<string> ResolveStoredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a language the current one. Synchronous on purpose: culture changes made inside an awaited
    /// call would not reach the caller's execution context. At start-up call it before the first component renders.
    /// </summary>
    void Apply(string culture);

    /// <summary>Applies and stores a language. Components already on screen keep their old texts until reloaded.</summary>
    Task SetAsync(string culture, CancellationToken cancellationToken = default);
}

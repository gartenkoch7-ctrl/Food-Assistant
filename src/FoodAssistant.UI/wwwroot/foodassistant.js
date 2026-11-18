// Browser glue of the Food-Assistant UI. Loaded synchronously in <head> so the stored theme is
// applied before the first paint (no flash of the wrong theme). Blazor calls the functions on
// window.foodAssistant through IBrowserInterop.
(function () {
    'use strict';

    // Must match BrowserThemePreferenceStore.StorageKey and ThemeModeTokens.
    var themeStorageKey = 'foodassistant.theme';
    var themeTokens = ['light', 'dark', 'high-contrast'];

    function applyTheme(token) {
        var root = document.documentElement;
        themeTokens.forEach(function (name) {
            root.classList.remove('theme-' + name);
        });
        // "system" (or anything unknown) leaves the class off: the CSS then follows prefers-color-scheme.
        if (themeTokens.indexOf(token) >= 0) {
            root.classList.add('theme-' + token);
        }
    }

    // Same cookie ASP.NET Core's request localization reads, so the server prerenders in the chosen language.
    function setCultureCookie(culture) {
        var value = encodeURIComponent('c=' + culture + '|uic=' + culture);
        var secure = window.location.protocol === 'https:' ? '; secure' : '';
        document.cookie = '.AspNetCore.Culture=' + value + '; path=/; max-age=31536000; samesite=lax' + secure;
    }

    try {
        applyTheme(window.localStorage.getItem(themeStorageKey));
    } catch (error) {
        // Storage is blocked (private mode, policy): the system theme applies.
    }

    function focusElement(id) {
        var element = document.getElementById(id);
        if (element) {
            element.focus();
        }
    }

    window.foodAssistant = {
        applyTheme: applyTheme,
        focusElement: focusElement,
        setCultureCookie: setCultureCookie
    };
})();

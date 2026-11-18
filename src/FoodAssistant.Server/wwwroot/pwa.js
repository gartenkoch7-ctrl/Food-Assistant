// Registers the service worker once the page has loaded, so it never competes with the first render.
// A missing or blocked service worker (private mode, http) is harmless: the app then simply is not cached.
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('/service-worker.js', { scope: '/' }).catch(function () {
            // Ignored on purpose: no offline shell, everything else works.
        });
    });
}

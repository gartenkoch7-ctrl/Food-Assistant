// Service worker for the app shell only (ADR-0005: offline data is a later concern of the sync layer).
// It keeps styles, scripts, fonts, images and the manifest of this origin, so the installed app opens
// fast and without layout jumps. Pages, API calls and the Blazor runtime always go to the network.
'use strict';

const CACHE_PREFIX = 'foodassistant-shell-';
const CACHE_NAME = CACHE_PREFIX + 'v1';
const MAX_ENTRIES = 60;
const SHELL_DESTINATIONS = ['style', 'script', 'font', 'image', 'manifest'];
const NETWORK_ONLY_PREFIXES = ['/api/', '/_framework/', '/_blazor', '/health', '/hubs/'];

self.addEventListener('install', function () {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys()
            .then(function (names) {
                return Promise.all(names
                    .filter(function (name) { return name.startsWith(CACHE_PREFIX) && name !== CACHE_NAME; })
                    .map(function (name) { return caches.delete(name); }));
            })
            .then(function () { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function (event) {
    const request = event.request;
    if (request.method !== 'GET' || !isShellAsset(request)) {
        return; // Not ours: the browser handles it as usual.
    }
    event.respondWith(staleWhileRevalidate(event));
});

function isShellAsset(request) {
    const url = new URL(request.url);
    if (url.origin !== self.location.origin) {
        return false;
    }
    if (NETWORK_ONLY_PREFIXES.some(function (prefix) { return url.pathname.startsWith(prefix); })) {
        return false;
    }
    return SHELL_DESTINATIONS.indexOf(request.destination) >= 0 || url.pathname.endsWith('.webmanifest');
}

// Answer from the cache at once and refresh it in the background. Fingerprinted files never change under
// the same name; the few that do (manifest, service worker neighbours) catch up on the next visit.
function staleWhileRevalidate(event) {
    const request = event.request;
    return caches.open(CACHE_NAME).then(function (cache) {
        return cache.match(request).then(function (cached) {
            const refreshed = fetch(request)
                .then(function (response) {
                    if (!response.ok) {
                        return response;
                    }
                    // Keep the worker alive until the copy is stored, and drop old fingerprinted files.
                    const stored = cache.put(request, response.clone()).then(function () { return trim(cache); });
                    event.waitUntil(stored);
                    return response;
                })
                .catch(function () {
                    return cached || Response.error();
                });
            return cached || refreshed;
        });
    });
}

// Fingerprinted file names change with every release, so the cache would grow forever without a cap.
// Cache keys come back in insertion order: the oldest entries go first.
function trim(cache) {
    return cache.keys().then(function (keys) {
        return Promise.all(keys.slice(0, Math.max(0, keys.length - MAX_ENTRIES)).map(function (key) {
            return cache.delete(key);
        }));
    });
}

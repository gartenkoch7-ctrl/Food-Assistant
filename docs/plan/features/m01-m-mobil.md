# M1 M – Android-App (Host, Offline, Gerät)

.NET MAUI Blazor Hybrid mit den gemeinsamen UI-Komponenten (ADR-0001): Kopplung mit dem Server,
Anmeldung per OIDC Code + PKCE (ADR-0003, Client `foodassistant-mobile`, Redirect
`foodassistant://auth/callback` aus `Server/Auth/MobileClientRegistration.cs`), Tokens in
`SecureStorage`; Offline-Cache (SQLite) mit Änderungswarteschlange für die Einkaufsliste (ADR-0005);
lokale Erinnerungen (ADR-0006); nativer Barcode-Scan. Verteilung zunächst als APK (ADR-0007).

**Status: blockiert durch F13** (Android-SDK Plattform 36 und JDK ≥ 17 fehlen auf dieser Maschine;
MAUI-Workloads sind installiert). Spuren M1 → M2 → M3, jeweils eigener Branch.
Vor Start: Detailplan gegen Stand nach M1a/E2 prüfen.

## Aufgaben
- [ ] M0-Vorbereitung (Nutzer): F13 umsetzen; `dotnet build src/FoodAssistant.Mobile -f net10.0-android` läuft
- [ ] M1 Host, Kopplung, Anmeldung, Online-Betrieb, Live mit Token
- [ ] M2 Offline-Cache, Delta-Sync, Warteschlange Einkaufsliste
- [ ] M3 Lokale Erinnerungen, nativer Scanner, APK-Build in CI

## Detailplan

### Paket M1 – Host und Anmeldung

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** F13
- **Branch:** `feat/m01-m1-mobile-host`
- **Muster:** Host so dünn wie `src/FoodAssistant.Web.Client/Program.cs`; Token-Fluss wie
  `tests/FoodAssistant.Server.Tests/Auth/OidcFlow.cs`; Dienste hinter Client-Interfaces.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Mobile/FoodAssistant.Mobile.csproj` | neu | `net10.0-android` (+ `net10.0-ios` nur `Condition="$([MSBuild]::IsOSPlatform('osx'))"`), `UseMaui`, `ApplicationId` `org.foodassistant.app` (Nutzer bestätigt beim Start der Spur), Referenzen UI + Client | `dotnet build -f net10.0-android` | 50 |
| `src/FoodAssistant.Mobile/MauiProgram.cs`, `App.xaml(.cs)`, `MainPage.xaml(.cs)`, `wwwroot/index.html`, `Platforms/Android/*` (MainActivity, AndroidManifest mit Intent-Filter `foodassistant://auth`, Kamera-/Benachrichtigungsrechte) | neu | Blazor-WebView mit `Routes` aus UI; `AddFoodAssistantUi()`, `AddFoodAssistantClient(serverUri)` mit **Bearer** statt Antiforgery | – | ~8×40 |
| `src/FoodAssistant.Mobile/Pairing/ServerConnectionStore.cs` + `PairingPage.razor` | neu | Server-URL aus Einladungs-QR (`…/invite#token=`) oder Eingabe; Prüfung `GET /api/v1/info` (Mindest-App-Version → Hinweis „App aktualisieren“) | `PairingTests` (Mobile.Tests, ohne Gerät) | 60+120 |
| `src/FoodAssistant.Mobile/Auth/OidcLogin.cs` + `TokenStore.cs` + `BearerTokenHandler.cs` | neu | `WebAuthenticator.AuthenticateAsync` mit PKCE (S256), Token-Tausch `/connect/token`, Refresh vor Ablauf, `SecureStorage`; `BearerTokenHandler` (DelegatingHandler) ersetzt `AntiforgeryHandler`; Abmelden widerruft | `OidcLoginTests` (PKCE-Werte, Refresh-Logik mit Fake-Handler) | 150+60+70 |
| `src/FoodAssistant.Client/Composition/ClientRegistration.cs` | ändern | Option `UseBearer(Func<CancellationToken, Task<string?>>)` statt Antiforgery; `EnableLive` | `ClientRegistrationTests` | +20 |
| `src/FoodAssistant.Client/Sync/SignalRLiveUpdates.cs` | ändern | `AccessTokenProvider` bei Bearer | – | +5 |
| `src/FoodAssistant.Server/Sync/HubAccessTokenFromQuery.cs` + `Composition/LiveRegistration.cs` | neu/ändern | Access-Token aus Query `access_token` **nur** für Pfad `HouseholdHubContract.Path` an die OpenIddict-Validierung übergeben (eigener Handler im Validierungs-Event `ProcessAuthentication`); Query wird in Telemetrie bereits geschwärzt | `HubTokenTests` (Server.Tests: Hub mit Bearer per Query verbindet; anderer Pfad ignoriert Query-Token) | 50 |
| `tests/FoodAssistant.Mobile.Tests/*` | neu | Tests für plattformunabhängige Teile (Projekt `net10.0`, verlinkt nur die reinen Klassen) | ja | – |
| `tests/FoodAssistant.Architecture.Tests/Layers.cs` + `LayerDependencyTests.cs` | ändern | Mobile in die Schichtregeln (darf UI, Client, Contracts; nicht Domain/Application/Infrastructure) | ja | +15 |
| `FoodAssistant.slnx`, `.github/workflows/*` | ändern | Projekte; CI-Job `android-build` (`setup-java` 21, `dotnet workload install maui-android`) | CI grün | +30 |
| `docs/kb/build-und-tooling.md` (neu, README verlinken) | neu | Android-Voraussetzungen, Befehle, Gerät per USB | – | 50 |

### Paket M2 – Offline

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** M1, Merge E2 · **Branch:** `feat/m01-m2-mobile-offline`
- **Pakete:** `sqlite-net-pcl` (MIT) + `SQLitePCLRaw.bundle_green` (Apache-2.0) – Entscheidung nach ADR-0005 „im Detailplan“:
  kleiner als EF Core SQLite, schneller Start, kein Modell-Duplikat (Cache speichert DTO-JSON).

**Modell:** Tabelle `CachedEntity(EntityType, Id, HouseholdId, Json, Sequence)`, `SyncState(HouseholdId, Token)`,
`OutboxEntry(Id, HouseholdId, CreatedAt, Method, Path, BodyJson, IdempotencyKey, Attempts, LastError)`.
**Lesen:** `CachedCatalogClient`, `CachedInventoryClient`, `CachedShoppingClient` dekorieren die HTTP-Clients:
online → HTTP, Ergebnis in Cache; offline → Cache. **Schreiben offline nur Einkaufsliste**
(anlegen, `PUT`, `DELETE` laut ADR-0005): lokal anwenden (Version unverändert lassen, `BaseVersion` merken),
Outbox-Eintrag mit **festem** Idempotency-Key; alles andere offline → `ApiError(client.offline)` und UI-Hinweis.
**Senden:** `OutboxSender` bei Verbindung, in Reihenfolge, je Eintrag Antwort (`ShoppingItemChangeResult`) in den
Cache übernehmen; 4xx außer 408/429 → Eintrag verwerfen + Hinweis; 5xx/Netz → später erneut (Backoff).
**Sync:** beim Start, nach `Resynced`, alle 5 min im Vordergrund: `ISyncClient.GetChangesAsync` (E2), 410 → Vollstand.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Offline/IOfflineCache.cs`, `IOutbox.cs`, `IConnectivityState.cs` | neu | Schnittstellen im Client (AGENTS.md 2.2: Offline-Schnittstellen im Client) | – | 3×25 |
| `src/FoodAssistant.Client/Offline/CachedShoppingClient.cs`, `CachedInventoryClient.cs`, `CachedCatalogClient.cs`, `OutboxSender.cs`, `SyncCoordinator.cs` | neu | Logik oben, plattformunabhängig → testbar in Client.Tests mit In-Memory-Fakes | `CachedShoppingClientTests`, `OutboxSenderTests` (Reihenfolge, Idempotenz, Konfliktergebnis übernehmen), `SyncCoordinatorTests` (Delta, 410) | 5×150 |
| `src/FoodAssistant.Mobile/Offline/SqliteOfflineCache.cs`, `SqliteOutbox.cs`, `MauiConnectivityState.cs` | neu | Implementierungen; Datenbank je Haushalt geleert bei Abmeldung/Wechsel (datenschutz.md) | Gerätetest manuell | 3×120 |
| `src/FoodAssistant.UI/Common/OfflineBanner.razor` | neu | Hinweis „Offline – nur Einkaufsliste änderbar“ + Anzahl wartender Änderungen | `OfflineBannerTests` | 50 |

### Paket M3 – Gerät und Verteilung

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** M2 · **Branch:** `feat/m01-m3-mobile-device`
- **Pakete:** `Plugin.LocalNotification` (MIT), `BarcodeScanning.Native.Maui` (MIT).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Notifications/INotificationScheduler.cs` | neu | `Task RescheduleAsync(IReadOnlyList<ExpiryReminder> reminders, CancellationToken)` (ADR-0006) | – | 20 |
| `src/FoodAssistant.Mobile/Notifications/LocalExpiryNotificationScheduler.cs` + `ReminderPlanner.cs` | neu | nach jedem Sync: aus Cache-Chargen je Tag eine Sammelmeldung für die nächsten 14 Tage (≤ 64 geplant, iOS-Grenze), Uhrzeit/Vorlauf aus den Benachrichtigungseinstellungen (H1) bzw. Standard 8:00/2 Tage; Texte wie H1 | `ReminderPlannerTests` (plattformfrei) | 120+80 |
| `src/FoodAssistant.Mobile/Scan/NativeBarcodeScanner.cs` | neu | implementiert `IBarcodeScanner` (C2); Kamera-Berechtigung | manuell | 100 |
| `.github/workflows/android-release.yml` | neu | APK bauen und signieren (Keystore aus GitHub-Secret, **legt der Nutzer an**), an GitHub-Release hängen | – | 60 |
| `docs/kb/build-und-tooling.md`, `docs/kb/datenschutz.md` | ändern | Release-Ablauf; lokale Benachrichtigungen (keine Daten verlassen das Gerät) | – | +20 |

**Akzeptanz M (gesamt):** APK auf eigenem Handy: koppeln per Einladungs-QR, anmelden, Vorrat und
Liste sehen; Flugmodus: Liste abhaken und ergänzen; wieder online: Änderungen auf dem Server, Konflikte
nach ADR-0005; Erinnerung erscheint zur eingestellten Zeit; Scan bucht ein. TalkBack-Prüfung nach Checkliste.

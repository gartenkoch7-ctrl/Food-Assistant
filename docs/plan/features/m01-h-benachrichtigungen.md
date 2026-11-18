# M1 H – Benachrichtigungen (Web Push, ntfy)

Tägliche Erinnerung „läuft bald ab / abgelaufen“ pro Person zur eingestellten Uhrzeit in der
Haushaltszeitzone, über Web Push (VAPID, Schlüssel erzeugt der Server selbst) und optional ntfy.
Kein FCM/APNs, keine E-Mails (ADR-0006). Lokale Benachrichtigungen der App: Spur M3.
Texte laut KB [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md) (Verbrauchsdatum deutlicher).

Branch `feat/m01-h-notifications` · M1b · Pakete: H1 → H2. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] H1 Server: Einstellungen, VAPID, Abos, Versand (Web Push, ntfy), Tagesjob
- [ ] H2 UI: Einstellungsseite, Push-Anmeldung, Service Worker

## Detailplan

### Paket H1 – Server

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** F10 (nur ntfy-Teil; Web Push läuft)
- **Muster:** Geheimnis-Speicher wie `Server/Auth/DataProtectedTokenKeyStore.cs` (Data Protection);
  Hintergrunddienst wie `Application/Restock/RecurringItemsJob.cs`; Lokalisierung im Server über
  `IStringLocalizer<T>` + `.resx` wie `UI/Shell/ShellText.cs`.

**Pakete (Begründung im Commit):** `Lib.Net.Http.WebPush` (MIT; VAPID + verschlüsselter Versand).
**Kein** `WebPush` (MPL-2.0, Copyleft-artig).

**Daten (Migration `M1Notifications`):**
- `NotificationSettings` (PK `UserId`+`HouseholdId`, `IHouseholdOwned`): `ExpiryEnabled bool` (Standard true),
  `LeadDays int` (Standard 2, 0–14), `SendAt TimeOnly` (08:00), `QuietFrom/QuietTo TimeOnly?`,
  `Culture` (`de`/`en`), `NtfyTopic` (64)?, `NtfyTokenProtected` (Data Protection, Zweck `FoodAssistant.Ntfy`)?, `Version`.
- `PushSubscriptions` (Id, `UserId`, `Endpoint` (2048, unique), `P256dh`, `Auth`, `CreatedAt`,
  `LastSuccessAt?`, `FailureCount int`) – personenbezogen, kein Haushalt.
- `NotificationDispatches` (Id, `HouseholdId`, `UserId`, `Kind` (32), `ForDate DateOnly`; Unique `(HouseholdId, UserId, Kind, ForDate)`).
- `ServerSecrets` (`Name` PK (64), `ProtectedValue`, `CreatedAt`) – für den VAPID-Privatschlüssel.

**Ablauf Tagesjob (alle 5 min):** je Haushalt → je Mitglied mit Einstellungen (ohne Zeile = Standard)
und `ExpiryEnabled`: `jetzt (Haushaltszeit) ≥ SendAt`, nicht in Ruhezeit, kein Versand für heute →
Chargen mit Status `Soon` (Vorlauf = `LeadDays` der Person) oder `Expired` mit Ablauf **heute
oder gestern** → eine Sammelmeldung (Titel „3 Lebensmittel laufen bald ab“; Text: bis zu 5
„Artikelname – morgen“, Verbrauchsdatum mit „Verbrauchen bis …“ zuerst); leere Liste → nichts senden,
aber Versand vermerken. Push 404/410 → Abo löschen; andere Fehler `FailureCount+1`, ab 5 löschen.
**ntfy (F10 a):** Basis-URL nur aus `FoodAssistant:Ntfy:BaseUrl` (Umgebungsvariable), Person wählt Thema
(`^[A-Za-z0-9_-]{1,64}$`) und optional Token; `POST {BaseUrl}/{Topic}` mit `Title`, `Priority`
(`high` bei Verbrauchsdatum), keine Weiterleitungen, 10 s Zeitlimit.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Notifications/ExpiryDigest.cs` + `ExpiryDigestItem.cs` | neu | `static ExpiryDigest Build(IEnumerable<ExpiryDigestItem> items, DateOnly today, int leadDays, int maxLines = 5)`: Auswahl, Sortierung (UseBy zuerst, dann Datum), Zählung | `ExpiryDigestTests` | 70+15 |
| `src/FoodAssistant.Application/Notifications/INotificationSender.cs`, `NotificationMessage.cs`, `INotificationStore.cs`, `IVapidKeyProvider.cs` | neu | `Task<SendResult> SendAsync(NotificationRecipient, NotificationMessage, CancellationToken)`; Records; Store-Methoden für Einstellungen, Abos, Versandprotokoll | – | 4×25 |
| `src/FoodAssistant.Application/Notifications/NotificationText.cs` + `.resx` + `.en.resx` | neu | Titel/Zeilen DE/EN (Formulierungen aus der KB); Texte in der Sprache aus `NotificationSettings.Culture` über `IStringLocalizer<NotificationText>` mit gesetzter `CultureInfo` | `NotificationTextTests` (Vollständigkeit) | 5+2×20 |
| `src/FoodAssistant.Application/FoodAssistant.Application.csproj` | ändern | `PackageReference Microsoft.Extensions.Localization` (Version zentral vorhanden; auch J nutzt es) | – | +1 |
| `src/FoodAssistant.Application/Notifications/NotificationSettingsService.cs` | neu | `GetAsync(householdId, userId)`, `UpdateAsync(…)` (Validierung), `SubscribeAsync(userId, endpoint, p256dh, auth)` (nur `https`-Endpunkte), `UnsubscribeAsync`, `SendTestAsync` | über Endpunkt-Tests | 150 |
| `src/FoodAssistant.Application/Notifications/ExpiryNotificationRunner.cs` + `ExpiryNotificationJob.cs` | neu | Ablauf oben (Runner für aktiven Haushalt, Job über `IHouseholdScopeFactory`) | `ExpiryNotificationRunnerTests` (Fake-Sender, feste Zeit, Zeitzone, Ruhezeit, kein Doppelversand) | 150+70 |
| `src/FoodAssistant.Application/Notifications/NotificationsLog.cs` | neu | `DigestSent` (3600, Information, `HouseholdId`, `UserId`, `Channel`, `ItemCount`), `SendFailed` (3601, Warning, `Channel`, `StatusCode`), `SubscriptionRemoved` (3602, Information, `Reason`), `VapidKeyCreated` (3603, Information) – keine Produktnamen | – | 35 |
| `src/FoodAssistant.Infrastructure/Notifications/*` | neu | Entitäten + Konfigurationen (4), `NotificationStore`, `WebPushSender` (Lib.Net.Http.WebPush, `Subject` = `FOODASSISTANT__PUBLICURL`), `NtfySender` (typisierter HttpClient, `AllowAutoRedirect = false`), `VapidKeyProvider` (ECDsa P-256 erzeugen, öffentlicher Schlüssel Base64Url, privater geschützt in `ServerSecrets`), Optionen `NtfyOptions` | `WebPushSenderTests`/`NtfySenderTests` mit Fake-Handler; `VapidKeyProviderTests` (einmal erzeugt, danach gleich) | ~9×80 |
| Migration `M1Notifications` | neu (generiert) | siehe Daten | `MigrationTests` | gen. |
| `src/FoodAssistant.Server/Notifications/NotificationEndpoints.cs` | ändern (Stub) | Haushalt: `GET/PUT /notifications/settings` (Any, je Person), `POST /notifications/test` (Any) | – | 70 |
| `src/FoodAssistant.Server/Notifications/NotificationApiEndpoints.cs` | ändern (Stub) | `/api/v1/notifications`: `GET /web-push/public-key` (angemeldet), `POST /web-push/subscriptions`, `DELETE /web-push/subscriptions?endpoint=` (angemeldet, Antiforgery bei Cookie) | – | 60 |
| `src/FoodAssistant.Contracts/Notifications/*` | neu | `NotificationSettingsDto`, `UpdateNotificationSettingsRequest`, `PushSubscriptionRequest(string Endpoint, string P256dh, string Auth)`, `VapidPublicKeyDto(string PublicKey)`, `NotificationErrorCodes` | – | 5×12 |
| `src/FoodAssistant.Contracts/Common/ServerFeaturesDto.cs`, `Server/Api/Info/InfoEndpoints.cs` | ändern (Hotspot) | Feld `bool Ntfy` (BaseUrl gesetzt) | `InfoEndpointTests` | +3 |
| `tests/FoodAssistant.Server.Tests/Notifications/*` | neu | Einstellungen (eigene Person, fremder Haushalt 404), Abo nur https, Test-Versand, Job über API-Zeitsteuerung | ja | 3×150 |
| `docs/kb/datenschutz.md` | ändern | Einstellungen, Push-Abos (Endpoint ist personenbezogen), Versandprotokoll (Aufbewahrung 30 Tage → in E2-Aufbewahrung ergänzen), ntfy-Token verschlüsselt; Inhalt der Meldungen (Artikelname + Datum) | – | +5 |

### Paket H2 – UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** H1 · **Muster:** A2-Seiten; JS-Modul wie `UI/wwwroot/scan.js` (C2).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Notifications/INotificationClient.cs` + `NotificationClient.cs`, Registrierung | neu | alle H1-Endpunkte | `NotificationClientTests` | 20+70 |
| `src/FoodAssistant.UI/wwwroot/push.js` | neu | `supported()`, `subscribe(publicKey)` (`Notification.requestPermission`, `registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey })`), `unsubscribe()`; liefert `{endpoint, p256dh, auth}` | manuell | 70 |
| `src/FoodAssistant.Server/wwwroot/service-worker.js` | ändern | `push`-Ereignis → `showNotification(title, { body, tag: "expiry", data: { url: "/" } })`; `notificationclick` → Fenster fokussieren/öffnen | manuell | +30 |
| `src/FoodAssistant.UI/Notifications/NotificationSettingsPage.razor` + `NotificationsText.cs` + resx | neu | `@page "/settings/notifications"`: Schalter Ablauf-Erinnerung, Vorlauf, Uhrzeit, Ruhezeit, Sprache der Meldungen; „Benachrichtigungen in diesem Browser aktivieren“ (Status: nicht unterstützt / blockiert / aktiv); ntfy-Abschnitt nur bei `Features.Ntfy`; „Test senden“ | `NotificationSettingsPageTests` | 200+5+2×40 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Benachrichtigungen“ im Abschnitt „Haushalt“ | `MorePageTests` | +5 |
| `tests/FoodAssistant.UI.Tests/Notifications/*` + `Support/FakeNotificationClient.cs` | neu | bUnit | ja | 150+60 |

**Barrierefreiheit:** Checkliste. **Akzeptanz:** Tests grün; Sichtprüfung im Browser-Pane:
Push aktivieren, „Test senden“ zeigt Meldung; Zeit auf jetzt stellen → Sammelmeldung einmal.

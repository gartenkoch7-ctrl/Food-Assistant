# M1 K – Konto- und Haushalts-UI

Web-Oberfläche für das, was M0 nur als API hat: Einrichtungsassistent, Einladung annehmen,
Haushalt wechseln, Mitglieder und Einladungen verwalten, Haushaltseinstellungen (Name, Zeitzone,
Vorlauf „läuft bald ab“). ADR-0002, ADR-0003. Anmeldung/Abmelden liefert S0.5.

Branch `feat/m01-k-accounts-ui` · Pakete: K1 → K2.

## Aufgaben
- [ ] Bekannte Lücke aus Review S0 (bewusst offen): Haushaltswechsel während eines 401-Neuladens kann den alten Haushalt wieder aktivieren (`SessionState.SelectHouseholdAsync` erhöht die Generation nicht). Beim Bau der Haushaltsauswahl lösen: Speicher erst innerhalb des Übergangs lesen.
- [ ] K1 Server: Haushaltseinstellungen lesen/ändern
- [ ] K2 Client und UI: Einrichtung, Einladung annehmen, Haushalt & Mitglieder, Einladungen mit Link/QR

## Detailplan

### Paket K1 – Haushaltseinstellungen (Server)

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Muster:** `Application/Households/HouseholdService.cs`,
  `HouseholdValidation.cs` (Name, Zeitzone), `Server/Households/MemberEndpoints.cs`; Store-Muster `docs/kb/datenmodell.md`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Households/HouseholdSettingsDto.cs`, `UpdateHouseholdRequest.cs` | neu | `HouseholdSettingsDto(Guid Id, string Name, string TimeZone, int ExpiryLeadDays, long Version)`; `UpdateHouseholdRequest(string Name, string TimeZone, int ExpiryLeadDays)` | – | 2×10 |
| `src/FoodAssistant.Application/Households/IHouseholdSettingsStore.cs` | neu | `GetAsync(Guid householdId, …)` → `HouseholdSettingsDto?`; `UpdateAsync(Guid householdId, long? expectedVersion, string name, string timeZone, int leadDays, …)` → `OperationResult<HouseholdSettingsDto>` | – | 20 |
| `src/FoodAssistant.Application/Households/HouseholdSettingsService.cs` | neu | `GetAsync`, `UpdateAsync` (Validierung über `HouseholdValidation` + `ExpiryLeadDays` 0–14; Fehlercode `households.validation_failed`) | über Endpunkt-Tests | 70 |
| `src/FoodAssistant.Infrastructure/Households/HouseholdSettingsStore.cs` | neu | EF auf `Households` (Id = Route), Store-Muster | – | 70 |
| `src/FoodAssistant.Application/Composition/AccountsRegistration.cs`, `Infrastructure/Composition/AccountsInfrastructureRegistration.cs` | ändern | je eine Zeile | – | +2 |
| `src/FoodAssistant.Server/Households/HouseholdSettingsEndpoints.cs` | ändern (Stub) | `GET ""` (Any) → Einstellungen + ETag; `PUT ""` (Owner, If-Match Pflicht) – auf der Haushaltsgruppe `/households/{householdId}` | – | 50 |
| `tests/FoodAssistant.Server.Tests/Accounts/HouseholdSettingsTests.cs` | neu | lesen (alle Rollen), ändern nur Besitzer (Mitglied 403), Validierung, 412, fremder Haushalt 404, neue Vorlaufzeit wirkt auf `IHouseholdCalendar` | ja | 160 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json` | ändern | Snapshot | – | gen. |

### Paket K2 – Client und UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** K1 · **Muster:** `UI/Session/LoginPage.razor` (S0.5), `UI/Shell/MorePage.razor`,
  bestehende Endpunkte `Server/Auth/SetupEndpoints.cs`, `InvitationAcceptEndpoints.cs`,
  `Server/Households/InvitationEndpoints.cs`, `MemberEndpoints.cs`.

**Einladungslink:** `{Origin}/invite#token={token}` – Token im **Fragment**, damit er nie in
Server-Logs oder Proxys landet. QR-Code enthält denselben Link (Server-Adresse inklusive, ADR-0002).
QR als SVG im Browser über **QRCoder** (MIT; `PngByteQRCode` nicht nötig, `SvgQRCode`); Paket in
`Directory.Packages.props` (Begründung: Einladung per QR, später Etiketten J).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Accounts/IAccountClient.cs` + `AccountClient.cs` | ändern | `SetupAsync(SetupRequest)`, `AcceptInvitationAsync(AcceptInvitationRequest)` | `AccountClientTests` erweitern | +30 |
| `src/FoodAssistant.Client/Households/IHouseholdClient.cs` + `HouseholdClient.cs` | neu | Einstellungen (K1), Mitglieder (Liste, Rolle ändern, entfernen), Einladungen (anlegen, Liste, widerrufen) | `HouseholdClientTests` | 30+110 |
| `src/FoodAssistant.Client/Composition/HouseholdsClientRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.UI/Accounts/AccountsText.cs` + `.resx` + `.en.resx` | neu | alle Texte inkl. `Error.accounts.*`, `Error.households.*`, Rollen-Namen | – | 5+2×80 |
| `src/FoodAssistant.UI/Accounts/SetupPage.razor` | neu | `@page "/setup"` (öffentlich): Anzeigename, Benutzername, Passwort (+ Wiederholung), Haushaltsname, Zeitzone (`MudAutocomplete` über `TimeZoneInfo.GetSystemTimeZones()` IANA-Ids, Vorauswahl `TimeZoneInfo.Local.Id`, sonst `Europe/Berlin`); nach Erfolg automatisch anmelden (`LoginAsync`) und zu `/` | `SetupPageTests` | 150 |
| `src/FoodAssistant.UI/Accounts/InviteAcceptPage.razor` | neu | `@page "/invite"` (öffentlich): Token aus `NavigationManager.Uri`-Fragment; angemeldet → „Haushalt beitreten“; nicht angemeldet → Formular neues Konto (Anzeigename, Benutzername, Passwort) **oder** Link „Ich habe ein Konto – anmelden“ (`/login?returnUrl=/invite` – Fragment bleibt im Browser erhalten über `sessionStorage`-Zwischenspeicher via `IBrowserInterop.SetItemAsync`); Fehler (abgelaufen, widerrufen, benutzt) verständlich | `InviteAcceptPageTests` | 170 |
| `src/FoodAssistant.UI/Accounts/HouseholdSettingsPage.razor` | neu | `@page "/settings/household"`: Abschnitt „Haushalt wechseln“ (Radiogruppe über `SessionState.Me.Households`, `SelectHouseholdAsync`); Einstellungen (Besitzer bearbeitbar, sonst lesend); Mitglieder (Name, Rolle als Text; Besitzer: Rolle ändern, entfernen mit Bestätigung, Fehler „letzter Besitzer“); Einladungen (Besitzer) → `InvitationPanel` | `HouseholdSettingsPageTests` | 220 |
| `src/FoodAssistant.UI/Accounts/InvitationPanel.razor` | neu | Rolle wählen, „Einladung erstellen“ → Link + QR (`QrCodeSvg`) + „Link kopieren“ (Clipboard über `navigator.clipboard` in eigenem kleinen JS-Modul `UI/wwwroot/accounts.js`) + Ablaufdatum; Liste offener Einladungen mit Widerrufen | `InvitationPanelTests` | 150 |
| `src/FoodAssistant.UI/Accounts/QrCodeSvg.razor` | neu | `[Parameter] string Text`: `new SvgQRCode(QRCodeGenerator.GenerateQrCode(Text, ECCLevel.M)).GetGraphic(4)` als `MarkupString`; `role="img"` mit `aria-label` | `QrCodeSvgTests` | 35 |
| `src/FoodAssistant.UI/Session/SessionGate.razor` | ändern | keine – öffentliche Pfade `/setup`, `/invite` sind in S0.5 schon freigegeben (nur prüfen) | – | 0 |
| `tests/FoodAssistant.UI.Tests/Accounts/*` + `Support/FakeHouseholdClient.cs` | neu | bUnit | ja | 5×120+100 |
| `docs/kb/datenschutz.md` | ändern | „Einladungs-Token nur im URL-Fragment, nie an den Server außer im Annahme-Request“; `sessionStorage`-Eintrag `foodassistant.invite` bis zur Annahme | – | +2 |

**Barrierefreiheit:** Checkliste (Formulare mit Labels und Feldfehlern, QR mit Textalternative = Link).
**Akzeptanz:** Tests grün; Sichtprüfung auf frischer Datenbank: `/setup` → angemeldet; Einladung
erstellen, in privatem Fenster annehmen; Haushalt wechseln; Rolle ändern; Vorlauf ändern wirkt auf Status.

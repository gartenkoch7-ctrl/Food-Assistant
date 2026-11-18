# M1 E1 – Live-Updates im Web

SignalR-Client für den Haushalts-Hub (S0.2): verbinden nach Haushaltswahl, automatische
Wiederverbindung, danach „neu laden“-Signal an die Seiten, sichtbarer und angesagter
Verbindungsstatus. Seiten (A, B2, D) reagieren bereits auf `ILiveUpdates` (S0.4) – E1 liefert die
echte Implementierung. ADR-0005 „Live-Updates“.

Branch `feat/m01-e1-live` · ein Paket.

## Aufgaben
- [x] `ILiveUpdates.StartAsync`-Vertrag einhalten (Review S0, siehe XML-Doku): schnell zurückkehren, nie werfen, nicht auf den Handshake oder auf `Changed`/`Resynced`-Handler warten – `SessionState` ruft es unter seiner Übergangssperre auf (sonst blockiert Abmelden bzw. Deadlock). Test: Start bei nicht erreichbarem Hub blockiert `SignOutAsync` nicht.
- [x] Entfernte Mitglieder sofort aus der Hub-Gruppe nehmen (Review S0.2): Verbindungen pro Person merken, bei `RemoveMemberAsync`/Kontosperre aus `household:{id}` entfernen bzw. Verbindung beenden; Test „entferntes Mitglied bekommt keine Änderungen mehr“. Umgesetzt über `IMembershipRevocationNotifier` (Application, Hook in `MemberService.RemoveAsync`) + `HouseholdConnectionRegistry`; eine Kontosperre gibt es noch nicht (nur befristete Login-Sperre), dort entfällt der Hook.
- [x] E1 `SignalRLiveUpdates`, Statusanzeige, Tests

## Detailplan

### Paket E1 – Live-Client

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Blockiert durch:** –
- **Muster:** Hub-Vertrag `src/FoodAssistant.Contracts/Sync/HouseholdHubContract.cs` (S0.1);
  Hub-Tests `tests/FoodAssistant.Server.Tests/Sync/HouseholdHubTests.cs` (S0.2); UI-Dienste wie
  `src/FoodAssistant.UI/Theme/ThemeState.cs` (Event + `InvokeAsync(StateHasChanged)`).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Directory.Packages.props`, `src/FoodAssistant.Client/FoodAssistant.Client.csproj` | ändern | `Microsoft.AspNetCore.SignalR.Client` (Version aus S0.2) im Client-Projekt (läuft in WebAssembly; Begründung: offizieller Client, MIT) | – | +1 |
| `src/FoodAssistant.Client/Sync/SignalRLiveUpdates.cs` | neu | `public sealed class SignalRLiveUpdates(HttpClient http /* BaseAddress */, ILogger<SignalRLiveUpdates> logger) : ILiveUpdates, IAsyncDisposable`: `HubConnectionBuilder().WithUrl(new Uri(baseAddress, HouseholdHubContract.Path))` (Browser sendet Cookies selbst), `.WithAutomaticReconnect([0, 2, 5, 10, 30] s, danach alle 30 s)`; `On<HouseholdChangesMessage>(Changed, …)` → nur Nachrichten des aktiven Haushalts weiterreichen; `Reconnected` → erneut `Join(aktiver Haushalt)`, dann `Resynced` auslösen; `Closed` → Status `Disconnected`, nach 30 s neuer Startversuch; `StartAsync(householdId)`: verbinden falls nötig, alten Haushalt `Leave`, neuen `Join`; `HubException` beim Join → Status `Disconnected` + Log; Zustand + `StateChanged` | `LiveClientTests` (Server.Tests, echter Hub über `factory.Server.CreateHandler()`, LongPolling): erhält Änderung, ignoriert anderen Haushalt, Haushaltswechsel, Join ohne Mitgliedschaft | 180 |
| `src/FoodAssistant.Client/Sync/SyncLog.cs` | neu | `LiveConnectionLost` (4100, Information, –), `LiveReconnected` (4101, Information), `LiveJoinFailed` (4102, Warning, `ErrorCode`) | – | 25 |
| `src/FoodAssistant.Client/Composition/ClientRegistration.cs` | ändern | `ILiveUpdates` → `SignalRLiveUpdates` (scoped, ersetzt `NoLiveUpdates`); nur registrieren, wenn `OperatingSystem.IsBrowser()` **oder** Option `EnableLive` (Mobil setzt sie in M1) – auf dem Server bleibt der Platzhalter | – | +6 |
| `src/FoodAssistant.UI/Sync/LiveStatusIndicator.razor` | neu | kleiner Hinweis in der Kopf-/Seitenleiste: nur sichtbar bei `Reconnecting`/`Disconnected` („Verbindung unterbrochen – Daten können veraltet sein“), Symbol + Text, `role="status"`; Zustandswechsel über `ILiveAnnouncer` ansagen (nur Wechsel, nicht jede Wiederholung) | `LiveStatusIndicatorTests` | 70 |
| `src/FoodAssistant.UI/Sync/LiveText.cs` + `.resx` + `.en.resx` | neu | Texte | – | 5+2×10 |
| `src/FoodAssistant.UI/Shell/MainLayout.razor` | ändern | `<LiveStatusIndicator />` einfügen (einzige Änderung) | `MainLayoutTests` | +2 |
| `tests/FoodAssistant.Server.Tests/FoodAssistant.Server.Tests.csproj` | ändern | Projektreferenz auf `FoodAssistant.Client` | – | +1 |
| `docs/kb/telemetrie.md` | ändern | Client-Logs | – | +3 |

**Barrierefreiheit:** Live-Änderungen dezent (`aria-live="polite"`), Status nie nur Farbe.
**Akzeptanz:** Tests grün; Sichtprüfung mit zwei Browser-Tabs: Produkt in Tab 1 ändern → Tab 2
aktualisiert; Server neu starten → Hinweis erscheint, verschwindet nach Wiederverbindung, Seite lädt neu.
**Nicht Teil:** Delta-Abruf (E2), Mobil-Token (M1).

# M1 – MVP Vorrat & Einkauf

Ziel: Ein Haushalt kann Food-Assistant im Alltag nutzen – im Web anmelden, Produkte und Lagerorte
pflegen, Chargen mit Haltbarkeit erfassen (Barcode/Open Food Facts oder manuell), verbrauchen und
entsorgen, mehrere Einkaufslisten mit Läden und gelernter Reihenfolge führen, live gemeinsam abhaken,
den Einkauf in den Vorrat buchen, Mindestbestand und wiederkehrende Artikel automatisch auf die
Liste setzen. Danach (M1b): Erinnerungen, Statistik, Etiketten, Import, Backup/Export, Verlauf und
die Android-App mit Offline-Einkaufsliste.

Integrationsbranch: `feat/m01-mvp` (von `feat/m00-foundation`). Spuren zweigen davon ab.
Grundlagen: ADR-0002, 0004, 0005, 0006, 0007, 0010; KB [vorrat-und-haltbarkeit](../kb/vorrat-und-haltbarkeit.md),
[einkauf](../kb/einkauf.md), [externe-daten](../kb/externe-daten.md), [datenschutz](../kb/datenschutz.md).

**Phasen:** M1 wird in **M1a** (Kern Vorrat & Einkauf im Web) und **M1b** (Erweiterungen und
Mobil-App) geteilt. M1a ist allein nutzbar und wird zuerst abgenommen; M1b-Spuren starten erst
nach der Akzeptanz M1a, weil sie auf deren Dateien aufbauen (Exporte, Delta-Sync, Statistik lesen
die M1a-Daten). Vor dem Start jeder M1b-Spur prüft die steuernde Sitzung ihren Detailplan gegen
den Stand nach M1a (Dateinamen, Signaturen) – ca. 15 min, Stufe 3.

## Feature-Liste

### M1a – Kern (Web)
- [ ] [S0 – Schritt 0: Datenmodell, Nahtstellen, Client- und UI-Basis](features/m01-s0-schritt0.md)
- [ ] [A – Produktkatalog und Lagerorte](features/m01-a-katalog.md)
- [ ] [B1 – Haltbarkeitsregeln (Standard + Überschreibungen)](features/m01-b1-haltbarkeitsregeln.md)
- [ ] [B2 – Chargen, Zustände und Verbrauch](features/m01-b2-chargen-verbrauch.md)
- [ ] [C1 – Open Food Facts (Server, Cache)](features/m01-c1-open-food-facts.md)
- [ ] [C2 – Scan-Ablauf im Web](features/m01-c2-scan.md)
- [ ] [D – Einkaufslisten, Läden, gelernte Reihenfolge](features/m01-d-einkauf.md)
- [ ] [E1 – Live-Updates im Web](features/m01-e1-live.md)
- [ ] [F – Einkauf abschließen](features/m01-f-einkauf-abschliessen.md)
- [ ] [G – Mindestbestand und wiederkehrende Artikel](features/m01-g-nachkauf.md)
- [ ] [K – Konto- und Haushalts-UI (Einrichtung, Einladung, Mitglieder)](features/m01-k-konto-ui.md)

### M1b – Erweiterungen und Mobil
- [ ] [E2 – Delta-Sync und Aufbewahrung](features/m01-e2-delta-sync.md)
- [ ] [H – Benachrichtigungen (Web Push, ntfy)](features/m01-h-benachrichtigungen.md)
- [ ] [I – Verschwendungsstatistik](features/m01-i-statistik.md)
- [ ] [J – Etiketten (PDF)](features/m01-j-etiketten.md)
- [ ] [L – Import Grocy und CSV](features/m01-l-import.md)
- [ ] [N – Backup und Haushaltsexport](features/m01-n-backup-export.md)
- [ ] [V – Aktivitätsverlauf](features/m01-v-verlauf.md)
- [ ] [M – Android-App (Host, Offline, Gerät)](features/m01-m-mobil.md)

## Querschnittsentscheidungen dieses Plans (technisch, keine Nutzerfrage)

- **Domain bleibt persistenzfrei:** EF-Entitäten in Infrastructure; die Charge wird über
  `StockBatchSnapshot` (Domain) gelesen und geschrieben (`StockBatch.Restore` / `StockBatchSnapshot.From`).
- **Ein Datenmodell für M1a in Schritt 0** (eine Migration `M1Core`), damit M1a-Spuren keine
  Migrationen erzeugen und der `AppDbContextModelSnapshot` keine Konflikte bekommt.
- **Ergebnis-Typ für Anwendungsfälle:** `OperationResult<T>` (Application) und eine gemeinsame
  Abbildung auf HTTP (`OperationResults.ToHttpResult`, Server) – keine Fehlerabbildung pro Endpunkt.
- **Sync-Nahtstelle zentral:** Ein `SaveChangesInterceptor` schreibt für jede synchronisierte
  Entität einen `ChangeLogEntry` und meldet nach dem Commit an `IHouseholdChangeNotifier`
  (SignalR). Spuren markieren Entitäten nur mit `ISyncTracked`.
- **Hintergrunddienste** arbeiten pro Haushalt über `IHouseholdScopeFactory` (setzt den
  Haushaltsfilter), nie über `IgnoreQueryFilters`.
- **„Heute“** kommt nur aus `IHouseholdCalendar` (Zeitzone des Haushalts, `TimeProvider`).
- **Vorlauf „läuft bald ab“** ist eine Haushaltseinstellung (`ExpiryLeadDays`, Standard 2); der
  Vorlauf für Erinnerungen ist pro Person (ADR-0006).
- **Web ohne Vorrendern:** `InteractiveWebAssemblyRenderMode(prerender: false)`; der Server müsste
  sonst für jede Datenseite die Cookie-Anmeldung an sich selbst weiterreichen. Das Theme-Skript
  läuft weiter synchron, ein Lade-Zustand überbrückt den ersten Aufruf.
- **UI nur gegen Interfaces aus Client** (`ICatalogClient`, `IInventoryClient`, `IShoppingClient`
  …): die Mobil-App ersetzt sie später durch Cache-/Offline-Varianten (ADR-0005).
- **Einkaufslisten-Einträge** nutzen eine eigene `long Version` (Konfliktregeln ADR-0005), alle
  anderen Entitäten `xmin` als Version/ETag.
- **Eingeschränkte Rolle:** Einträge ergänzen und abhaken, sonst nur lesen (ADR-0002).

## Parallelisierung

```
                 ┌─ A Katalog ───────────┐
                 ├─ B1 Regeln ───────────┤
                 ├─ B2 Chargen ──────────┤        ┌─ C2 Scan ─┐
S0 (Schritt 0) ──┼─ C1 Open Food Facts ──┼─Merge──┼─ F Abschl.┼─Merge── Akzeptanz M1a
  Backend-Kette  ├─ D Einkauf ───────────┤ Welle1 └─ G Nachkauf┘ Welle2
  ∥ Frontend-    ├─ E1 Live ─────────────┤
    Kette        └─ K Konto-UI ──────────┘

M1b (nach Akzeptanz M1a):
  ┌─ E2 Delta-Sync ──────────────┐
  ├─ I Statistik ─ V Verlauf ────┤
  ├─ H Benachrichtigungen ───────┼─ Merge ── M (Android: M1 → M2 → M3, blockiert F13) ── Akzeptanz M1
  ├─ J Etiketten (F9) ───────────┤
  ├─ L Import (F10) ─────────────┤
  └─ N Backup/Export (F11, F12) ─┘
```

### Spuren

| Spur | Inhalt | Branch | Start | Wartet auf | Pakete (Modell/Effort) |
|---|---|---|---|---|---|
| S0 | Schritt 0 | `feat/m01-mvp` (direkt) | sofort | F1, F3 (Annahme = Empfehlung) | S0.1 Opus/high, S0.2 Opus/high, S0.3 Haiku/low, S0.4 Sonnet/medium, S0.5 Sonnet/medium |
| A | Produkte, Barcodes, Kategorien, Lagerorte | `feat/m01-a-catalog` | nach S0 | – | A1 Sonnet/medium, A2 Sonnet/medium |
| B1 | Standardregeln, Überschreibungen | `feat/m01-b1-shelf-life` | nach S0 | B1.1 blockiert F2 (Merge mit leerer Tabelle möglich) | B1.1 Opus/high, B1.2 Sonnet/medium, B1.3 Sonnet/medium |
| B2 | Chargen, Zustände, Verbrauch, Rückgängig | `feat/m01-b2-stock` | nach S0 | B2.3 (UI) auf Merge A | B2.1 Sonnet/medium, B2.2 Opus/high, B2.3 Sonnet/medium |
| C1 | Open Food Facts, Cache, Barcode-Abfrage | `feat/m01-c1-food-facts` | nach S0 | F7, F8 (Merge) | C1 Sonnet/medium |
| D | Listen, Einträge, Konfliktregeln, Läden, Reihenfolge | `feat/m01-d-shopping` | nach S0 | D3 (UI) auf Merge A; F3 | D1 Opus/high, D2 Sonnet/medium, D3 Sonnet/medium |
| E1 | SignalR-Client, Wiederverbindung, Live-Anzeige | `feat/m01-e1-live` | nach S0 | – | E1 Sonnet/medium |
| K | Einrichtung, Einladung, Haushalt/Mitglieder, Einstellungen | `feat/m01-k-accounts-ui` | nach S0 | – | K1 Sonnet/medium, K2 Sonnet/medium |
| C2 | Scan-Seite (Kamera, manuell), Einbuchen/Verbraucht | `feat/m01-c2-scan` | Welle 2 | Merge A, B2, C1; F5, F6 | C2 Sonnet/medium |
| F | Einkauf abschließen | `feat/m01-f-checkout` | Welle 2 | Merge B2, D; F5 | F1 Opus/high, F2 Sonnet/medium |
| G | Mindestbestand, wiederkehrende Artikel | `feat/m01-g-restock` | Welle 2 | Merge A, B2, D | G1 Sonnet/medium, G2 Sonnet/medium |
| E2 | Delta-Feed, Tombstones, Aufbewahrung | `feat/m01-e2-delta-sync` | M1b | Akzeptanz M1a | E2 Opus/high |
| H | Einstellungen, Web Push, ntfy, Tagesjob | `feat/m01-h-notifications` | M1b | Akzeptanz M1a; ntfy-Teil F10 | H1 Opus/high, H2 Sonnet/medium |
| I | Verschwendungsstatistik | `feat/m01-i-statistics` | M1b | Akzeptanz M1a | I Sonnet/medium |
| J | Etiketten-PDF mit QR | `feat/m01-j-labels` | M1b | F9 | J Sonnet/medium |
| L | Import CSV, Grocy | `feat/m01-l-import` | M1b | L2: F10 | L1 Sonnet/medium, L2 Opus/high |
| N | DB-Backup, Haushaltsexport/-import | `feat/m01-n-backup-export` | M1b | N1: F11, N2: F12 | N1 Sonnet/medium, N2 Opus/high |
| V | Aktivitätsverlauf-Ansicht | `feat/m01-v-activity` | M1b | F4, Merge E2 (Aufbewahrung) | V Sonnet/medium |
| M | Android: Host + Anmeldung, Offline, Gerät | `feat/m01-m1-mobile-host`, `feat/m01-m2-mobile-offline`, `feat/m01-m3-mobile-device` | M1b | F13; M2 auf Merge E2 | M1 Opus/high, M2 Opus/high, M3 Sonnet/medium |

**Pakete gesamt:** M1a 25 (S0 5, A 2, B1 3, B2 3, C1 1, C2 1, D 3, E1 1, F 2, G 2, K 2),
M1b 13 (E2 1, H 2, I 1, J 1, L 2, N 2, V 1, M 3) – zusammen **38**.

**Schritt 0 intern:** Backend-Kette S0.1 → S0.2 → S0.3 und Frontend-Kette S0.4 → S0.5. Die
Frontend-Kette startet nach S0.1 (braucht deren Contracts) und läuft parallel zu S0.2/S0.3
(disjunkte Dateien) in einem Worktree; die steuernde Sitzung führt beide in `feat/m01-mvp` zusammen.
Review-Schleife §9 über ganz Schritt 0, dann erst starten die Spuren.

**Spuren mit mehreren Paketen** laufen im eigenen Branch **nacheinander** (je Paket ein Subagent
mit dem Modell des Pakets). Ein UI-Paket, das „auf Merge A wartet“, holt vor Beginn den
Integrationsbranch in seinen Branch (`git merge feat/m01-mvp`).

### Event-ID-Bereiche

Präfix = Namespace `FoodAssistant.<Projekt>.<Feature>`; die längste Übereinstimmung gilt
(`TelemetryConventionTests`). Alle Zeilen trägt **S0.3** auf einmal in `s_ranges` und in
`docs/kb/telemetrie.md` ein – Spuren ändern die Tabelle nicht. UI und Client teilen je Feature
denselben Bereich (wie M0).

| Feature-Namespace | Spur | Server | Infrastructure | Application | UI + Client |
|---|---|---|---|---|---|
| `Sync` | S0, E1, E2 | 1300–1324 | 2200–2249 | 3100–3149 | 4100–4149 |
| `Activity` | S0, V | 1325–1349 | 2250–2299 | 3150–3199 | 4150–4199 |
| `Households` (Server) / `Session`, `Accounts` (UI, Client) | K, S0 | 1350–1374 | (M0 2000–2199) | (M0 3000–3099) | 4200–4249 |
| `Catalog` | A | 1375–1399 | 2300–2349 | 3200–3249 | 4250–4299 |
| `ShelfLife` | B1 | 1400–1424 | 2350–2399 | 3250–3299 | 4300–4349 |
| `Inventory` | B2 | 1425–1449 | 2400–2449 | 3300–3349 | 4350–4399 |
| `FoodFacts` | C1 | 1450–1474 | 2450–2499 | 3350–3399 | – |
| `Scan` | C2 | – | – | – | 4400–4449 |
| `Shopping` | D | 1475–1499 | 2500–2549 | 3400–3449 | 4450–4499 |
| `Stores` | D | 1500–1524 | 2550–2599 | 3450–3499 | 4500–4549 |
| `Checkout` | F | 1525–1549 | 2600–2649 | 3500–3549 | 4550–4599 |
| `Restock` | G | 1550–1574 | 2650–2699 | 3550–3599 | 4600–4649 |
| `Notifications` | H | 1575–1599 | 2700–2749 | 3600–3649 | 4650–4699 |
| `Statistics` | I | 1600–1624 | 2750–2799 | 3650–3699 | 4700–4749 |
| `Labels` | J | 1625–1649 | 2800–2849 | 3700–3749 | 4750–4799 |
| `Import` | L | 1650–1674 | 2850–2899 | 3750–3799 | 4800–4849 |
| `Backup` | N | 1675–1699 | 2900–2949 | 3800–3849 | (Export) |
| `Export` | N | 1700–1724 | 2950–2999 | 3850–3899 | 4850–4899 |
| `FoodAssistant.Mobile` (ganzes Projekt) | M | – | – | – | 4900–4999 |

Server-Endpunkte loggen selten (Logs gehören in Application); die kleinen Server-Bereiche reichen.

### Migrationen

| Name | Spur | Inhalt |
|---|---|---|
| `M1Core` | S0.1 | alle M1a-Tabellen, `Households.ExpiryLeadDays` |
| `M1E2SyncRetention` | E2 | Tabelle `SyncRetention` (Stand der Aufräumung je Haushalt) |
| `M1Notifications` | H1 | Benachrichtigungseinstellungen, Push-Abos, Versandprotokoll, Server-Geheimnisse |

Regeln:
- Erzeugen nur mit `dotnet ef migrations add <Name> --project src/FoodAssistant.Infrastructure --startup-project src/FoodAssistant.Server`.
- Braucht eine Spur **ungeplant** eine Schemaänderung: nicht selbst entscheiden, sondern melden
  (Eskalation §13.3). Gibt die steuernde Sitzung sie frei, heißt die Migration `M1<Spur><Was>`
  (z. B. `M1DProductSection`).
- **Beim Merge** einer Spur mit Migration verwirft die steuernde Sitzung deren Migration samt
  Snapshot-Konflikt und erzeugt sie in `feat/m01-mvp` mit gleichem Namen neu (Zeitstempel danach).
  `MigrationTests.StartupMigration_CreatesSchema_AndModelMatchesMigrations` muss grün sein.

### Exklusiv-Dateien

Nach Schritt 0 gehören die von S0 angelegten Feature-Dateien (Stubs, Entitäten, Contracts) der
jeweiligen Spur. Pfade relativ zu `src/` bzw. `tests/`; `<P>` = jedes Projekt.

- **S0:** alles, was S0 anlegt oder ändert (siehe Feature-Datei), bis zum Abschluss von Schritt 0.
- **A:** `<P>/Catalog/**` (Domain, Contracts, Application, Infrastructure, Server, Client, UI),
  `*/Composition/Catalog*Registration.cs`, `tests/*/Catalog/**`, `tests/FoodAssistant.UI.Tests/Support/FakeCatalogClient.cs`.
- **B1:** `<P>/ShelfLife/**`, `*/Composition/ShelfLife*Registration.cs`, `tests/*/ShelfLife/**`,
  `Infrastructure/FoodAssistant.Infrastructure.csproj` (nur die `EmbeddedResource`-Zeile).
- **B2:** `<P>/Inventory/**` (auch die M0-Domain-Dateien dort, v. a. `StockBatch.cs`; neue wie
  `BatchMover.cs`), `*/Composition/Inventory*Registration.cs`, `tests/*/Inventory/**`,
  `UI/Inventory/**` (inkl. `StatusChip.razor`, `InventoryText*.resx`).
- **C1:** `<P>/FoodFacts/**` **außer** `Client/FoodFacts/**` (C2), `*/Composition/FoodFacts*Registration.cs`
  außer der Client-Registrierung, `tests/*/FoodFacts/**`, Abschnitt OFF in `Server/appsettings.json`.
- **D:** `<P>/Shopping/**`, `<P>/Stores/**`, `*/Composition/Shopping*Registration.cs`,
  `tests/*/{Shopping,Stores}/**`; Domain `Shopping/**` außer `ShoppingConflictResolver.cs` (nur lesen).
- **E1:** `Client/Sync/SignalRLiveUpdates.cs`, `Client/Sync/SyncLog.cs`, `Client/Composition/ClientRegistration.cs`,
  `Client/FoodAssistant.Client.csproj`, `UI/Sync/**`, `UI/Shell/MainLayout.razor`, `tests/FoodAssistant.Server.Tests/Sync/LiveClient*.cs`.
- **K:** `UI/Accounts/**`, `Client/Accounts/**`, `Client/Households/**`, `Client/Composition/HouseholdsClientRegistration.cs`,
  `Server/Households/HouseholdSettingsEndpoints.cs`, `Application/Households/{IHouseholdSettingsStore,HouseholdSettingsService}.cs`,
  `Infrastructure/Households/HouseholdSettingsStore.cs`, `Application/Composition/AccountsRegistration.cs`,
  `Infrastructure/Composition/AccountsInfrastructureRegistration.cs`, `Contracts/Households/{HouseholdSettingsDto,UpdateHouseholdRequest}.cs`,
  `UI/wwwroot/accounts.js`, `tests/*/Accounts/**`.
- **Welle 2:** C2: `UI/Scan/**`, `Client/Scan/**`, `Client/FoodFacts/**`, `Client/Composition/FoodFactsClientRegistration.cs`,
  `UI/wwwroot/scan.js`, `UI/wwwroot/lib/zxing/**`, `tests/FoodAssistant.UI.Tests/Scan/**`. F: `<P>/Checkout/**`.
  G: `<P>/Restock/**`. Welle-2-Spuren dürfen **neue** Dateien in Ordnern gemergter Spuren anlegen
  (z. B. G `Domain/Inventory/RestockDecision.cs`, `Domain/Shopping/RecurrenceSchedule.cs`), bestehende
  Dateien dort nur lesen. A ruft die Bestands-Handler schon selbst auf; G ändert keine A-Datei.
- **M1b-Spuren:** je `<P>/<Feature>/**` laut Event-ID-Tabelle; zusätzlich E2: `HouseholdData*Registration.cs`
  (S0) und `Infrastructure/Sync/**` außer den Interceptoren; N: `Dockerfile`, `docker-compose.yml`,
  `.env.example`, `deploy/**`, `Infrastructure/Persistence/DatabaseMigrationService.cs`;
  L: `Client/Http/ApiHttpClient.cs` (Multipart-Erweiterung); H: `Server/wwwroot/service-worker.js`,
  `Application/FoodAssistant.Application.csproj`; M: `src/FoodAssistant.Mobile/**`, `tests/FoodAssistant.Mobile.Tests/**`,
  `Server/Sync/HubAccessTokenFromQuery.cs`, `Server/Composition/LiveRegistration.cs`, `Client/Offline/**`,
  `Client/Composition/ClientRegistration.cs`, `Client/Sync/SignalRLiveUpdates.cs`, `.github/**`,
  `tests/FoodAssistant.Architecture.Tests/{Layers,LayerDependencyTests}.cs`. Überschneidungen (E2/M bei `Client/Sync`)
  entfallen durch die Reihenfolge M nach E2.

Zwei Spuren, die dieselbe Exklusiv-Datei bräuchten, gibt es in diesem Plan nicht; taucht doch
eine auf, wird gemeldet und die spätere Spur wartet.

### Hotspots (nur ergänzen, nichts umschreiben)

| Datei | Wer ergänzt | Konfliktlösung beim Merge |
|---|---|---|
| `Directory.Packages.props` | jede Spur mit neuem Paket (Begründung im Commit) | beide Zeilen behalten, alphabetisch in der Gruppe |
| `FoodAssistant.slnx` | S0.4 (`Client.Tests`), M1 (`Mobile`, `Mobile.Tests`) | beide behalten |
| `tests/*/…Tests.csproj` | Spuren mit neuen Projekt-/Paketreferenzen | beide behalten |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json` | jede Spur mit Endpunkten | **nicht von Hand mergen**: nach dem Merge mit `UPDATE_OPENAPI_SNAPSHOT=1` neu erzeugen, Diff prüfen |
| `docs/kb/telemetrie.md` (Tabellen Logs/Abläufe/Messwerte) | jede Spur | Zeilen nach Name bzw. EventId einsortieren |
| `docs/kb/datenschutz.md`, `docs/kb/externe-daten.md`, `docs/kb/README.md` | C1, H, L, N, J, K | Zeilen behalten |
| `src/FoodAssistant.Server/appsettings*.json` | C1, H, N | Abschnitte behalten |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `Shell/ShellText*.resx` | **nur M1b**-Spuren (M1a-Links setzt S0.5) | Links behalten, Reihenfolge laut S0.5 |
| `Contracts/Common/ServerFeaturesDto.cs`, `Server/Api/Info/InfoEndpoints.cs` | H (`Ntfy`), M1 | Felder behalten |
| `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | nur E2, H (geplant) | siehe Migrationen |
| `Server/Program.cs` | nur S0, M1 | – |
| `docs/plan/m01-mvp.md` | nur steuernde Sitzung (Häkchen, Rückblick) | – |

Registrierungen (`ApplicationRegistration`, `InfrastructureRegistration`, `HouseholdEndpoints`,
`ClientRegistration`) sind **keine** Hotspots: S0.3 legt für jede Spur leere Einhängepunkte in
eigenen Dateien an und ruft sie dort schon auf.

### Merge-Reihenfolge

- **M1a Welle 1:** A → B1 → B2 → C1 → D → E1 → K.
  (A zuerst, weil B2/D-UI den `ProductPicker` brauchen; B1 vor B2, damit die echten Regeln den
  Standard-Platzhalter ersetzen; E1 und K sind unabhängig und dürfen bei Verzug nach vorn.)
- **M1a Welle 2:** C2 → F → G.
- **M1b:** E2 → I → V → H → J → L → N → M1 → M2 → M3 (blockierte Spuren rutschen ans Ende).
- Nach jedem Merge: `dotnet build FoodAssistant.slnx`, `dotnet test --solution FoodAssistant.slnx`,
  `./scripts/check-file-length.ps1`; OpenAPI-Snapshot neu erzeugen, wenn er Konflikte hatte.
- Nach jeder Welle: Sichtprüfung im Browser-Pane (Anmeldung, Hauptabläufe der Welle).

## Akzeptanz M1a
- [ ] `dotnet build` ohne Warnungen, `dotnet test` grün (inkl. Testcontainers), Dateilängen grün
- [ ] Neue Installation: Einrichtung im Web, Anmeldung, Einladung per Link annehmen, Haushalt wechseln
- [ ] Lagerorte anlegen; Produkt per Barcode (Open Food Facts) und manuell anlegen
- [ ] Charge einbuchen, öffnen, einfrieren, auftauen, Rest anlegen – Datum/Art laut Regeln (Tests + Sichtprüfung)
- [ ] Verbrauchen (aufgebraucht, teilweise, entsorgt mit Grund), Scan „Verbraucht“ nach FEFO, Rückgängig
- [ ] Mehrere Listen, Laden zuordnen, Reihenfolge lernt aus dem Abhaken und ist manuell änderbar
- [ ] Zwei Browser: Abhaken erscheint live im anderen; Konfliktregeln ADR-0005 als API-Tests
- [ ] Einkauf abschließen bucht Chargen mit vorbelegten Daten; Freitext zuordnen oder überspringen
- [ ] Mindestbestand setzt automatisch auf die Standardliste (einmal, erneut erst nach Wiederauffüllen)
- [ ] Wiederkehrender Artikel erscheint zum Termin (Zeitzone des Haushalts), nicht doppelt
- [ ] Haushaltstrennung und Rollen (Eingeschränkt) für jeden neuen Endpunkt getestet
- [ ] Barrierefreiheits-Checkliste für jede neue Seite durchlaufen, Texte DE/EN vollständig
- [ ] Telemetrie-Katalog vollständig, keine Nutzerinhalte in Logs
- [ ] Gesamt-Review nach §9 ohne offene Befunde

## Akzeptanz M1b
- [ ] Delta-Feed liefert Änderungen und Löschungen; zu alter Token → volle Neuladung
- [ ] Web Push und (falls konfiguriert) ntfy: tägliche Erinnerung zur eingestellten Uhrzeit, Texte laut KB
- [ ] Statistik pro Monat (verbraucht vs. entsorgt, Gründe, Top-Produkte, Trend)
- [ ] Etiketten-PDF für Avery-Bögen mit QR-Code, QR öffnet die Charge
- [ ] CSV-Import und Grocy-Import mit Vorschau
- [ ] Tägliches DB-Backup + Backup vor Migrationen; Haushaltsexport/-import nach `docs/kb/export-schema.md`
- [ ] Aktivitätsverlauf (90 Tage) mit Rückgängig, falls F4 so entschieden
- [ ] Android-APK: Anmeldung per Kopplung, Einkaufsliste offline abhaken/ergänzen, Sync, lokale Erinnerungen, Scan
- [ ] Gesamt-Review nach §9 ohne offene Befunde; Merge nach `develop` nur mit Freigabe des Nutzers

## Rückblick
_(nach dem Meilenstein: Einstufungen zu hoch/zu niedrig, Nahtstellen, Dauer je Spur)_

## Offene Fragen

Jede Frage mit Optionen und Empfehlung. **Annahme** = womit gearbeitet wird, falls die Antwort
bis zum Start des betroffenen Pakets fehlt (nur wo das gefahrlos rückbaubar ist). Blockierte
Pakete starten bzw. mergen erst nach der Antwort.

**F1 – Produktkategorien: feste Liste oder frei anlegbar?**
- a) Feste, mitgelieferte Liste (~24 Schlüssel, DE/EN übersetzt); Haltbarkeitsregeln und
  Ladenbereich hängen an der Kategorie. b) Frei anlegbar pro Haushalt (eigene Tabelle, Regeln
  und Bereich pro eigener Kategorie nötig). c) Feste Liste + optionale eigene Unterkategorien später.
- **Empfehlung: a** (Regeln brauchen eine bekannte Kategorie; c bleibt später möglich).
- Betrifft: S0.1 (Annahme a), A, B1. Vorschlag der Liste steht in S0.1.

**F2 – Inhalt der Standard-Haltbarkeitsregeln: wer liefert und gibt frei?**
- a) Agent (Opus) entwirft die Tabelle aus öffentlichen Empfehlungen (BZfE, Verbraucherzentrale,
  BfR) mit Quelle pro Zeile, eher vorsichtig; **Nutzer prüft und gibt frei**. b) Nutzer liefert
  die Werte. c) M1 ohne Standardwerte, nur eigene Überschreibungen.
- **Empfehlung: a**; bis zur Freigabe bleibt die Tabelle leer (= „ohne Datum“, sicherer Rückfall).
- Betrifft: B1.1 (blockiert für den Merge der Daten; Code von B1 ist unabhängig).

**F3 – Ladenbereiche: feste Liste oder frei pro Laden?**
- a) Feste Liste (~12 Bereiche, DE/EN), Kategorie → Standardbereich, pro Produkt überschreibbar;
  pro Laden nur die Reihenfolge. b) Bereiche frei pro Laden (Zuordnung Artikel → Bereich je Laden).
- **Empfehlung: a** (Lernen braucht über Läden hinweg gleiche Schlüssel).
- Betrifft: S0.1 (Annahme a), D.

**F4 – Aktivitätsverlauf und „Rückgängig“ in M1?**
- a) M1a: „Rückgängig“ direkt nach der Aktion (Snackbar) für Verbrauch, Entsorgen und
  Zustandswechsel; M1b: Verlaufsseite (90 Tage, ADR-0002). b) Nur Snackbar-Rückgängig, Verlauf
  ab M2. c) Beides erst später.
- **Empfehlung: a**. Die Tabelle `HouseholdActivities` legt S0 in jedem Fall an (billig).
- Betrifft: B2.2/B2.3 (Annahme a), V (blockiert).

**F5 – Neue Charge direkt im Gefrierschrank: TK-Ware oder selbst eingefroren?**
- a) Mit Packungsdatum = gekaufte TK-Ware (Zustand verschlossen, Packungsdatum gilt); ohne Datum
  = selbst eingefroren (Regel „gefroren“ ab heute, gilt als Verbrauchsdatum). Im Dialog ein
  Schalter „selbst eingefroren“ zum Umstellen. b) Immer Regel „gefroren“. c) Immer Packungsdatum.
- **Empfehlung: a**.
- Betrifft: B2.2 (blockiert für diesen Fall; Rest von B2.2 läuft), F1, C2.

**F6 – Scan „Verbraucht“: welche Menge?**
- a) Einheit Stück/Packung/Dose: 1 abziehen; Masse/Volumen: die ganze früheste Charge als
  „aufgebraucht“; danach Snackbar mit „Rückgängig“ und „Menge ändern“. b) Immer ganze Charge.
  c) Immer Mengen-Dialog.
- **Empfehlung: a**.
- Betrifft: C2 (blockiert für den Verbraucht-Modus).

**F7 – Produktbilder von Open Food Facts?**
- a) M1 ohne Bilder. b) Server lädt ein kleines Vorschaubild einmal und liefert es selbst aus
  (Cache, Quellenangabe CC BY-SA). c) App lädt Bilder direkt von OFF (verletzt „nur der Server
  spricht mit OFF“, IP der Nutzer geht an OFF).
- **Empfehlung: a** (b in einem späteren Meilenstein).
- Betrifft: C1 (Annahme a).

**F8 – Kontaktangabe im `User-Agent` für Open Food Facts**
- OFF verlangt App-Name, Version und Kontakt. Welche öffentliche Projekt-URL (z. B. GitHub-Repo)
  soll eingetragen werden? Optionen: a) Repo-URL, b) Projekt-Website, c) nur konfigurierbar
  (Self-Hoster trägt eigene ein, Standard leer = kein Kontakt).
- **Empfehlung: a** als Standard, per `FoodAssistant__OpenFoodFacts__Contact` überschreibbar.
- Betrifft: C1 (Merge erst mit Wert).

**F9 – PDF-Bibliothek für Etiketten**
- a) PDFsharp 6 (MIT) + QRCoder (MIT), Schrift eingebettet (Atkinson Hyperlegible Next, OFL).
  b) QuestPDF (eigene Community-Lizenz, Umsatzgrenze für kommerzielle Nutzer – passt nicht sauber
  zu MIT-Selbsthosting). c) Eigenes minimales PDF ohne Bibliothek.
- **Empfehlung: a**.
- Betrifft: J (blockiert).

**F10 – Ausgehende Verbindungen zu Adressen, die Nutzer eingeben (Grocy-Import, ntfy)**
- Risiko: Der Server ruft Adressen im internen Netz ab (SSRF).
- a) ntfy-Server-URL nur per Umgebungsvariable (Server-Admin); Personen wählen nur Thema und
  Token. Grocy-URL gibt nur ein Besitzer pro Import ein, beliebige http(s)-Adresse (Grocy steht
  meist im LAN), keine Weiterleitungen, feste Grocy-Pfade, Zeit- und Größenlimit, API-Key wird nie
  gespeichert. b) Zusätzlich private Netze sperren (bricht LAN-Grocy). c) Grocy-Import nur per
  hochgeladener Datei.
- **Empfehlung: a**, in `datenschutz.md` dokumentiert.
- Betrifft: L2 (blockiert), H1 ntfy-Teil (blockiert; Web Push nicht).

**F11 – Weg für Datenbank-Backups (ADR-0007 lässt es offen)**
- a) `pg_dump` (postgresql-client-17 aus dem PGDG-Repo) im Server-Image; Backup täglich und vor
  Migrationen durch den Server. Kosten: größeres Image, `RUN` im Final-Stage (bei arm64-Builds
  emuliert). b) Hilfscontainer im Compose (z. B. eigener `postgres:17`-Container mit Cron);
  „Backup vor Migration“ dann nur über ein gemeinsames Signal, schwerer zuverlässig.
  c) Logisches Backup selbst über Npgsql `COPY` (viel Eigenbau).
- **Empfehlung: a**.
- Betrifft: N1 (blockiert).

**F12 – Haushaltsexport: Personenbezug und Import-Ziel**
- a) Export enthält nur Haushaltsdaten, **keine Personen** (Ereignisse ohne Person); Import nur
  durch den Server-Admin in einen **neuen** Haushalt (neue IDs), Importierender wird Besitzer.
  b) Export mit Anzeigenamen der Mitglieder (für den Verlauf). c) Import auch in bestehende
  Haushalte (Zusammenführen).
- **Empfehlung: a**.
- Betrifft: N2 (blockiert).

**F13 – Android-Werkzeuge fehlen (Nutzeraktion nötig)**
- Befund: MAUI-Workloads sind installiert (`maui-android 10.0.0`), aber das Android-SDK unter
  `C:\Program Files (x86)\Android\android-sdk` hat nur Plattform 31/33 und Build-Tools 32; Java
  ist JDK 11/8. .NET 10 für Android braucht Plattform 36 und mindestens JDK 17. Ein Gerät oder
  Emulator-Image fehlt ebenfalls.
- a) Nutzer installiert Microsoft OpenJDK 21 (Adminrechte) und erlaubt dem Agenten,
  `dotnet build -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory=%LOCALAPPDATA%\Android\Sdk -p:JavaSdkDirectory=<JDK21> -p:AcceptAndroidSDKLicenses=True`
  auszuführen (Lizenzannahme durch den Nutzer bestätigt); Test auf eigenem Handy per USB-Debugging.
  b) Wie a, zusätzlich Emulator-Image (~2 GB). c) Mobil auf M2 verschieben.
- **Empfehlung: a**; Signierschlüssel für Release-APKs erzeugt der Nutzer (GitHub-Secret).
- Betrifft: M1–M3 (blockiert).

**F14 – Ablaufdatum einer geöffneten/eingefrorenen Charge von Hand ändern?**
- Ein aus einer Regel berechnetes Datum gilt als Verbrauchsdatum (Sicherheitsgrenze).
- a) Datum/Art nur bei **verschlossenen** Chargen korrigierbar; sonst nur Menge, und die Regel
  lässt sich pro Produkt/Kategorie überschreiben. b) Immer korrigierbar, mit Hinweis.
- **Empfehlung: a**.
- Betrifft: B2.1/B2.2 (Annahme a).

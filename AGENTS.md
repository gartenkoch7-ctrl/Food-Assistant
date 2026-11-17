# AGENTS.md – Food-Assistant

> **Food-Assistant** ist ein **Open-Source-Server zum Selbsthosten** (MIT-Lizenz) für **einen
> Haushalt**: Vorräte und Haltbarkeit im Blick behalten, Einkäufe planen und verwalten, mit
> KI-Hilfe kochen – und weitere Funktionen rund ums Essen, sobald sie ins Produkt passen.
> Clients: Web (PWA), Android, iOS.

Diese Datei ist die **einzige Quelle der Wahrheit** für alle Agents (Claude Code, Codex,
Cursor, …) und Menschen. `CLAUDE.md` importiert sie nur.

> **Projektstand:** Stack entschieden ([ADR-0001](docs/adr/0001-tech-stack.md)), Code folgt.
> Skripte, Tests und Befehle, die noch nicht existieren, legt an, wer sie zuerst braucht.

---

## 1. Knowledgebase – vor jeder Aufgabe lesen

Lade nur die Dateien, die deine Aufgabe betreffen. Index: [docs/kb/README.md](docs/kb/README.md)
Fehlt eine Datei noch, legt sie an, wer das Thema als Erstes braucht.

| Thema | Datei | Lesen, wenn … |
|---|---|---|
| Produkt, Scope, MVP | [docs/kb/produkt-und-scope.md](docs/kb/produkt-und-scope.md) | du Features planst |
| Glossar | [docs/kb/glossar.md](docs/kb/glossar.md) | ein Begriff unklar ist (Vorrat, Charge, MHD, Verbrauchsdatum, …) |
| Architektur & Schichten | [docs/kb/architektur.md](docs/kb/architektur.md) | du Code anlegst/verschiebst |
| Datenmodell | [docs/kb/datenmodell.md](docs/kb/datenmodell.md) | du Entitäten, Speicherung oder Migrationen anfasst |
| Vorrat & Haltbarkeit | [docs/kb/vorrat-und-haltbarkeit.md](docs/kb/vorrat-und-haltbarkeit.md) | du an Bestand, Ablaufdaten, Erinnerungen arbeitest |
| Einkauf | [docs/kb/einkauf.md](docs/kb/einkauf.md) | du an Einkaufslisten oder Nachkauf-Logik arbeitest |
| KI-Assistent | [docs/kb/ki-assistent.md](docs/kb/ki-assistent.md) | du Prompts, Modelle oder KI-Funktionen anfasst |
| Externe Daten | [docs/kb/externe-daten.md](docs/kb/externe-daten.md) | du Produktdatenbanken, Barcodes oder APIs anbindest |
| Datenschutz | [docs/kb/datenschutz.md](docs/kb/datenschutz.md) | Nutzerdaten gespeichert, geloggt oder an Dritte gesendet werden |
| Barrierefreiheit | [docs/kb/barrierefreiheit.md](docs/kb/barrierefreiheit.md) | du UI baust – Checkliste pro Feature |
| Export-Schema | [docs/kb/export-schema.md](docs/kb/export-schema.md) | du Daten hinzufügst, die in den Haushaltsexport gehören |
| Testumgebung | [docs/kb/testumgebung.md](docs/kb/testumgebung.md) | du testest oder misst |
| Build & Tooling | [docs/kb/build-und-tooling.md](docs/kb/build-und-tooling.md) | Build oder Tests nicht laufen |
| **Telemetrie (Katalog)** | [docs/kb/telemetrie.md](docs/kb/telemetrie.md) | du ein Feature baust – jedes bringt Logs/Abläufe mit |
| Architekturentscheidungen | [docs/adr/README.md](docs/adr/README.md) | du eine Entscheidung infrage stellst; Rollen/Konten 0002, Anmeldung 0003, API 0004, Sync 0005, Benachrichtigungen 0006, Betrieb 0007, Sprache/Barrierefreiheit 0008 |
| **Roadmap & Meilensteinpläne** | [docs/plan/README.md](docs/plan/README.md) | du eine Aufgabe beginnst – arbeite im aktuellen Meilenstein, hake erledigte Punkte ab |
| Detailplan-Vorlage | [docs/plan/detailplan-vorlage.md](docs/plan/detailplan-vorlage.md) | du einen Plan in Pakete zerlegst und Modelle zuweist (§13) |

**Pflege:** Wer etwas Neues lernt (API-Eigenheit, Messwert, Verhaltensbeobachtung),
schreibt es in die passende KB-Datei. Neue Grundsatzentscheidungen → neues ADR
(`docs/adr/NNNN-titel.md`). Bestehende ADRs werden nicht umgeschrieben, sondern ersetzt
(`Status: Ersetzt durch NNNN`).

---

## 2. Harte Architekturregeln (nicht verhandelbar)

### 2.1 Dateigröße
- **Max. 300 Zeilen pro Datei** für Code und Tests (`*.cs`, `*.razor`, `*.razor.cs`, `*.css`,
  `*.js`, `*.ps1`, `*.sh`).
- Ausgenommen: generierte Dateien (`*.g.cs`, `*.Designer.cs`, EF-Core-Migrationen und
  `*ModelSnapshot.cs`, `bin/`, `obj/`, `wwwroot/lib/`), Lockfiles.
- Geprüft durch `scripts/check-file-length.ps1` – lokal im Pre-Commit-Hook und in CI.
- Nähert sich eine Datei 250 Zeilen: **jetzt** aufteilen, nicht später. Aufteilen nach
  Verantwortung (eigene Klasse/Unterkomponente), nicht mechanisch (`partial` nur für
  Razor-Code-Behind).
- Ein öffentlicher Typ bzw. eine Komponente pro Datei, Dateiname = Typname.

### 2.2 Schichten & Abhängigkeiten
```
Server ──► Infrastructure ──► Application ──► Domain
  │                               │
  │                               └──► Contracts ◄──┐
  └──► Web.Client ──► UI ──► Client ────────────────┘
       Mobile ─────────┘
```
- **Domain** kennt niemanden: reine Fachlogik (Vorrat, Haltbarkeit, Mengen und Einheiten,
  Einkaufslogik, Rezept-Modelle). Keine IO-, UI-, EF-Core-, HTTP- oder KI-Abhängigkeiten.
- **Contracts** kennt niemanden: API-DTOs `v1`, nur Daten, keine Logik.
- **Application** (→ Domain, Contracts) orchestriert Anwendungsfälle und definiert
  Schnittstellen (Repositories, KI, Produktdaten …), die **Infrastructure** implementiert.
- **Infrastructure** (→ Application): EF Core/PostgreSQL, KI-Anbieter, Open Food Facts.
- **Server** ist Composition Root (DI), API-Endpunkte und Host der Web-App. Keine Fachlogik.
- **Client** (→ Contracts): typisierter API-Client und Offline-Schnittstellen.
- **UI** (→ Client, Contracts): gemeinsame Razor-Komponenten für Web und Mobil. Kennt
  **weder Domain noch Application** – Web und App reden nur über die HTTP-API mit dem Server.
- **Web.Client** und **Mobile** sind dünne Hosts; Plattformcode (Kamera, Benachrichtigungen,
  SQLite-Cache) nur in **Mobile** hinter Schnittstellen aus Client.
- Erzwungen durch `tests/FoodAssistant.Architecture.Tests` (NetArchTest).
- Innerhalb eines Projekts: Ordner nach **Feature** (`Inventory/`, `Shopping/`, `Cooking/`,
  `Expiry/`), nicht nach Typart (`Services/`, `Helpers/`, `Utils/` sind verboten).

### 2.3 KI-Funktionen
- KI nur über `IChatClient` (Microsoft.Extensions.AI) bzw. eigene Schnittstellen in Application;
  Anbieter-Pakete (Ollama, Anthropic, OpenAI-kompatibel) nur in Infrastructure.
- KI ist **optional**: Jede Funktion muss ohne konfigurierten Anbieter sauber deaktiviert sein.
- Anbieter, Modell-IDs, Limits und Kosten stehen in **Konfiguration**, nicht im Code verstreut.
- Prompts liegen versioniert in eigenen Dateien (nicht als Strings quer durch den Code) und
  sind in `docs/kb/ki-assistent.md` beschrieben.
- An die KI geht nur, was die Funktion braucht (z. B. Vorratsliste, Rezeptwunsch) – keine
  Konto-, Kontakt- oder Standortdaten. Was gesendet wird, steht in `docs/kb/datenschutz.md`.
- KI-Ausgaben sind **Vorschläge**: Sie ändern Vorrat, Einkaufsliste oder Daten nie ohne
  Bestätigung durch den Nutzer. Strukturierte Antworten werden gegen ein Schema validiert.
- **Sicherheitsgrenze Lebensmittel:** Aussagen zu Haltbarkeit, Allergenen und Verzehr nach
  Ablauf kommen aus geprüften Regeln/Daten, nicht frei von der KI. KI-Texte dazu tragen einen
  Hinweis und widersprechen nie der Regel-Logik.
- Tests laufen gegen einen **Fake-Client**; echte KI-Aufrufe nur in gekennzeichneten,
  standardmäßig übersprungenen Integrationstests.

### 2.4 Daten & Zeit
- Mengen tragen ihre **Einheit im Typ** (`Quantity(value, unit)`), nie nackte Zahlen.
- Zeit nur über `TimeProvider`, nie `DateTime.Now`/`UtcNow`. Ablaufdaten sind `DateOnly`
  (ohne Uhrzeit), die Zeitzone des Haushalts ist eine Einstellung.
- Daten gehören immer einem **Haushalt**; jede Abfrage filtert nach Haushalt (EF-Core-Query-Filter),
  nie haushaltsübergreifend.
- Schemaänderungen nur über EF-Core-Migrationen; keine Datenverluste ohne Export-/Rückweg.
- Der **SQLite-Cache auf dem Handy** ist nur Cache und Änderungswarteschlange – die Wahrheit
  liegt im Server. Offline erlaubt: Vorrat und Einkaufsliste lesen, Einkaufsliste abhaken/ergänzen.
- Secrets (API-Keys, Verbindungsstrings) nie im Repo – nur über Umgebungsvariablen bzw.
  `dotnet user-secrets`; `.env.example` ohne Werte.
- API-Änderungen nur abwärtskompatibel innerhalb von `/api/v1`; Bruch → `/api/v2`.

### 2.5 Telemetrie
- **Jedes Feature** bringt seine Telemetrie mit: Logs über `ILogger<T>` mit
  `[LoggerMessage]`-Methoden (eine `*Log.cs` je Feature-Ordner, EventId im Bereich des Projekts),
  Abläufe als `Telemetry.Source.StartActivity("foodassistant.<feature>.<vorgang>")`, Messwerte über
  `Telemetry.Meter`. Alles steht im Katalog [docs/kb/telemetrie.md](docs/kb/telemetrie.md).
- Export über **OpenTelemetry**; OTLP-Ziel ist optional und wird vom Self-Hoster konfiguriert.
  Ohne Konfiguration verlassen keine Telemetriedaten den Server.
- **Keine Inhalte des Nutzers** in Logs oder Attributen: keine Namen, E-Mails, Produkt- oder
  Rezeptfreitexte, Prompts oder KI-Antworten, keine vollen Pfade – nur IDs, Zählwerte, Kategorien,
  Dauer, Status, Modellname, Token-Zahlen.
- KI-Aufrufe messen immer Dauer, Anbieter, Modell, Token-Zahlen und Fehlerart.
- Ruft der Client den Server auf, wird der Ablauf per `traceparent`-Header weitergegeben.
- Erzwungen durch `TelemetryConventionTests` und den Analyzer CA1848 (nur `[LoggerMessage]`).

### 2.6 Statischer Code und Dienste
- `static` nur für Code **ohne Nebenwirkungen**: reine Logik, Konstanten, Mappings,
  `[LoggerMessage]`-Klassen und `Telemetry`, `Program`, DI-Erweiterungen in `Composition/`.
- Alles mit Dateien, Datenbank, Netz, KI, Umgebungsvariablen, Uhr, Sprache, Kamera oder
  Benachrichtigungen ist ein **Dienst** (Interface + Implementierung, registriert in der
  Composition Root). Zeit über `TimeProvider`, Texte über `IStringLocalizer<T>`.

### 2.7 Querschnittsregeln (Details in den ADRs)
- **Haushalt & Rollen (ADR-0002):** jede Abfrage nach Haushalt gefiltert; Rechte nur über
  Policies (Server-Admin, Besitzer, Mitglied, Eingeschränkt). Fremder Haushalt → `404`.
- **Anmeldung (ADR-0003):** OpenIddict + Identity. Web nur per `HttpOnly`-Cookie (BFF), keine
  Tokens im Browser; App per PKCE, Tokens in `SecureStorage`. Externes OIDC verknüpft nur, legt
  nie Konten an. Keine E-Mails, kein 2FA/Passkey im ersten Ausbau.
- **API (ADR-0004):** `/api/v1`, ProblemDetails mit `errorCode`, UUIDv7, `ETag`/`If-Match`,
  `Idempotency-Key`, Cursor-Pagination, OpenAPI-Snapshot-Test. Endpunkte nur über die
  gemeinsamen Hilfen bauen.
- **Sync (ADR-0005):** offline nur Einkaufsliste schreibbar, Konfliktregeln als Domain-Logik;
  Live-Updates per SignalR, Hub nur für eigene Haushalte.
- **Benachrichtigungen (ADR-0006):** lokal (App), Web Push, optional ntfy. Kein FCM/APNs.
- **Betrieb (ADR-0007):** Infrastruktur per Umgebungsvariablen, Rest im Admin-Bereich;
  Geheimnisse mit Data Protection verschlüsselt. Migrationen nur vorwärts, beim Start nach
  Backup. Neue Daten → im Haushaltsexport berücksichtigen.
- **Sprache & Barrierefreiheit (ADR-0008):** jeder UI-Text in DE und EN; WCAG 2.2 AA,
  Checkliste [docs/kb/barrierefreiheit.md](docs/kb/barrierefreiheit.md).

---

## 3. Externe Daten & Lizenzen

- Produkt-, Nährwert- und Rezeptdaten Dritter (z. B. Open Food Facts) nur gemäß deren Lizenz
  nutzen; Quelle und Lizenz in `docs/kb/externe-daten.md` vermerken, Attribution in der App.
- Keine Rezepte, Bilder oder Texte von Webseiten ungeprüft übernehmen oder scrapen.
- Neue Datenquelle oder neuer Drittanbieter → kurzer Eintrag in `docs/kb/datenschutz.md`
  (was wird gesendet, wohin, warum).

---

## 4. Tech-Stack

| Bereich | Wahl |
|---|---|
| Sprache/Runtime | C# 14, **.NET 10 (LTS)** |
| Server | ASP.NET Core (Minimal APIs, `/api/v1`), hostet die Web-App |
| Web | Blazor Web App, *Interactive WebAssembly*, als PWA installierbar |
| Mobil | .NET MAUI Blazor Hybrid (Android, iOS) |
| UI | gemeinsame Razor-Klassenbibliothek, **MudBlazor**, Hell/Dunkel |
| Datenbank | **PostgreSQL** über EF Core (Npgsql); Mobil: SQLite nur als Offline-Cache |
| Anmeldung | ASP.NET Core Identity + **OpenIddict** (eigener OIDC-Anbieter) + optional externes OIDC |
| Live-Updates | SignalR (Hub pro Haushalt) |
| Benachrichtigungen | lokal (MAUI), Web Push (VAPID), optional ntfy |
| KI | Microsoft.Extensions.AI; Ollama, Anthropic Claude, OpenAI-kompatibel; abschaltbar |
| Produktdaten | Open Food Facts (Barcode) |
| Telemetrie | `ILogger` + `[LoggerMessage]`, `ActivitySource`/`Meter`, OpenTelemetry (OTLP optional) |
| Betrieb | Docker-Image + `docker-compose.yml` (Server + PostgreSQL) |
| Tests | xUnit v3 (Microsoft.Testing.Platform), NetArchTest, bUnit, Testcontainers |
| Lizenz | MIT |

Begründung: [ADR-0001](docs/adr/0001-tech-stack.md). Pakete zentral in
`Directory.Packages.props`. Neue NuGet-Pakete nur mit kurzer Begründung im PR; keine Pakete mit
Copyleft-Lizenz.

---

## 5. Code-Konventionen

- Code, Bezeichner, Kommentare, Commit-Messages: **Englisch**. Doku/KB: **Deutsch**.
- `Nullable` enabled, `TreatWarningsAsErrors` true, file-scoped Namespaces.
- Kommentare erklären das **Warum** (z. B. API-Eigenheiten, Haltbarkeitsregeln), nicht das Was.
- UI-Texte nie hart codiert, sondern über `IStringLocalizer`/`.resx` (Start: Deutsch, Englisch).
- Async durchgängig mit `CancellationToken`; kein `.Result`/`.Wait()`.
- EF-Core-Entitäten bleiben in Infrastructure; nach außen gehen Domain-Typen bzw. Contracts-DTOs.
- Jede native/externe Ressource ist `IDisposable` und wird deterministisch freigegeben.
- Commits: Conventional Commits (`feat:`, `fix:`, `perf:`, `docs:`, `refactor:`, `test:`).

---

## 6. Tests

- **Domain:** Unit-Tests für jede Logik (Haltbarkeit, Mengen/Einheiten, Einkaufslogik) – Ziel ≥ 90 %.
- **Architektur:** Schichtregeln aus 2.2 als Tests.
- **KI:** Fake-`IChatClient` mit festen Antworten; Schema-Validierung und Fehlerfälle
  (Timeout, ungültige Antwort, Kostenlimit, KI abgeschaltet) getestet.
- **Persistenz/API:** Integrationstests gegen echtes PostgreSQL per Testcontainers
  (`WebApplicationFactory`); Haushaltstrennung ausdrücklich getestet.
- **UI:** Komponenten mit bUnit.
- Infrastructure/UI: dünn halten, Logik nach Domain/Application ziehen, damit sie testbar ist.
- Testdateien unterliegen ebenfalls der 300-Zeilen-Regel.

---

## 7. Befehle

```powershell
dotnet build FoodAssistant.slnx                       # Build
dotnet test --solution FoodAssistant.slnx             # alle Tests (Docker für Testcontainers nötig)
docker compose up -d db                               # PostgreSQL für die Entwicklung
dotnet run --project src/FoodAssistant.Server         # Server + Web-App starten
dotnet build src/FoodAssistant.Mobile -f net10.0-android   # Android-App bauen
dotnet ef migrations add <Name> --project src/FoodAssistant.Infrastructure --startup-project src/FoodAssistant.Server
docker compose up -d --build                          # kompletter Self-Hosting-Stack
./scripts/check-file-length.ps1                       # 300-Zeilen-Regel prüfen
git config core.hooksPath .githooks                   # Pre-Commit-Hook aktivieren (einmalig)
```
iOS-Builds brauchen einen Mac (oder Cloud-Build).

---

## 8. Definition of Done

- [ ] `dotnet build` ohne Warnungen, `dotnet test` grün
- [ ] `check-file-length.ps1` grün (keine Datei > 300 Zeilen)
- [ ] Schichtregeln eingehalten (Architekturtests grün)
- [ ] Datenmodell geändert? → EF-Core-Migration, Haushaltsfilter, Integrationstest
- [ ] KI berührt? → Fake-Client-Tests, Prompt-Datei, Datenschutz-Eintrag, keine automatische Datenänderung, läuft auch mit KI aus
- [ ] Neue Endpunkte nach ADR-0004 (ProblemDetails/`errorCode`, Haushalts- und Rollenprüfung, Tests für fremden Haushalt und fehlende Rechte)
- [ ] UI geändert? → Texte DE/EN, Barrierefreiheits-Checkliste durchlaufen
- [ ] Neue Daten? → im Haushaltsexport und in `docs/kb/datenschutz.md` berücksichtigt
- [ ] Telemetrie mitgeliefert (nach 2.5) und im Katalog `docs/kb/telemetrie.md`
- [ ] Keine Secrets und keine Nutzerinhalte in Code, Logs oder Testdaten
- [ ] Neues Wissen in `docs/kb/`, neue Grundsatzentscheidung als ADR
- [ ] Code-Review-Schleife nach Abschnitt 9 ohne offene Befunde durchlaufen

---

## 9. Review, Commit, Push, Merge

1. **Vor jedem Commit ein Code-Review** der gesamten Änderung (z. B. `/code-review`).
2. **Führt das Review zu Änderungen**, wird danach wieder der **komplette** geänderte Code
   reviewt – nicht nur die Korrekturen.
3. Schritt 2 wiederholen, **bis ein Review keine Änderungen mehr ergibt**. Erst dann committen
   (Build, Tests und `check-file-length.ps1` vorher grün, siehe Abschnitt 8).
4. **Push** auf den eigenen Feature-Branch ist danach ohne Rückfrage erlaubt.
5. **Mergen** (in `develop`, `main` oder per PR) nur mit ausdrücklicher Genehmigung des Nutzers.

---

## 10. Selbstständig arbeiten

- **Probleme so weit wie möglich selbst lösen:** reproduzieren, Daten beschaffen (Logs, Traces,
  Testdaten, Settings), Ursache finden, beheben, testen, prüfen – ohne den Nutzer für
  Zwischenschritte zu brauchen. Nachfragen nur bei echten Entscheidungen (Produkt, Datenschutz,
  Kosten, Grundsatzfragen), nicht für Arbeit, die ein Agent selbst erledigen kann.
- **Werkzeuge nutzen:** App lokal starten und im Browser-Pane bzw. Emulator prüfen; Oberflächen
  per Computer-Use bedienen, wenn der Nutzer es erlaubt hat.
- **Kosten & Konten:** Kostenpflichtige KI-Aufrufe in größerem Umfang, neue Konten, API-Keys
  oder Cloud-Ressourcen nur nach Bestätigung durch den Nutzer. Nicht darum herumarbeiten.
- **Adminrechte:** Braucht ein Schritt Administratorrechte, zeigt der Agent den Schritt mit
  Begründung an, der Nutzer bestätigt ihn. Nicht ohne Bestätigung erhöhen.

---

## 11. Parallel arbeiten (Zeit sparen)

Bei **jedem** Plan und vor jeder Umsetzung prüfen: Was davon kann gleichzeitig laufen?

### Im Plan
- Jede Meilensteindatei hat einen Abschnitt **„Parallelisierung“** mit:
  - **Spuren:** je Spur Inhalt, Branch, Start, worauf sie wartet, Event-ID-Bereich
  - einem kleinen Ablaufbild
  - **Schritt 0:** gemeinsame Bausteine, die mehrere Spuren brauchen. Sie kommen zuerst und
    nacheinander.
  - **Exklusiv-Dateien:** gehören genau einer Spur
  - **Hotspots:** Dateien, die alle Spuren nur ergänzend ändern, etwa Einstellungen, Texte,
    Migrationen, DI-Registrierung, Telemetrie-Katalog
  - **Merge-Reihenfolge**
- Zwei Spuren, die dieselbe Exklusiv-Datei ändern müssen, werden **eine** Spur, die nacheinander
  arbeitet.
- **Event-ID-Bereiche** und **Migrationsnummern** werden im Plan je Spur vergeben, damit es keine
  Doppelbelegung gibt.

### In der Umsetzung
1. **Integrationsbranch** je Meilenstein (`feat/mNN-…` von `develop`). Die steuernde Sitzung
   setzt dort Schritt 0 um, mit Review-Schleife nach §9.
2. **Spuren** laufen als Subagenten mit eigenem Worktree (`isolation: worktree`) oder als eigene
   Sitzungen. Jede Spur:
   - hat einen eigenen Branch vom Integrationsbranch (`feat/mNN-<id>-…`)
   - hält Build, Tests, `check-file-length.ps1` und ihre eigene Review-Schleife (§9) ein
   - committet und pusht nur auf ihren Branch
   - hakt nur ihre eigene Feature-Datei ab
   - läuft mit dem Modell und Effort aus dem Detailplan (§13)
3. **Zusammenführen an einem Strang:** Die steuernde Sitzung mergt die Spuren in der
   Merge-Reihenfolge in den Integrationsbranch. Nach jedem Merge laufen Build und Tests, Konflikte
   in Hotspots löst sie dort.
4. Danach **Gesamt-Review** des ganzen Meilensteins (§9) und Akzeptanzprüfung. Der Merge nach
   `develop` braucht die Freigabe des Nutzers (§9.5).
5. Gleichzeitig laufen nur Spuren mit **getrennten Exklusiv-Dateien**. Im Zweifel nacheinander:
   Ein Merge-Konflikt kostet mehr als die gesparte Zeit.
6. Spuren des **nächsten** Meilensteins dürfen früher starten, wenn ihre Exklusiv-Dateien frei
   sind. Gemergt wird trotzdem erst in dessen Integrationsbranch.

---

## 12. Knapp antworten („Caveman“-Stil)

Spart Lesezeit und Tokens. Gilt für **Chat-Antworten** und **Berichte** von Nebensitzungen,
Spuren und Subagenten.

- Stichworte statt Sätze. Keine Füllwörter, keine Höflichkeitsfloskeln, keine Wiederholung der
  Aufgabe, kein „Ich werde jetzt …“.
- Ergebnis zuerst, dann nur, was der Nutzer wissen oder entscheiden muss: Branch, Commit,
  Testzahlen, offene Punkte, Risiken.
- Fachbegriffe, Datei- und Branchnamen, Zahlen und Befehle **exakt** lassen – Kürze nie auf Kosten
  von Genauigkeit.
- **Nicht** knapp: Code, Kommentare, Commit-Messages, Doku/KB/ADRs und Pläne folgen weiter
  Abschnitt 5. Warnungen (Datenverlust, Kosten, Adminrechte, Merge) bleiben vollständige,
  eindeutige Sätze.
- **Fragen nie im Caveman-Stil:** Jede Frage an den Nutzer (Rückfrage, Auswahl, Freigabe) samt
  ihren Antwortoptionen und Erklärungen wird in ganzen, verständlichen Sätzen gestellt.

---

## 13. Detailplan und Modellwahl (Zeit und Tokens sparen)

Vor der Umsetzung eines Meilenstein- oder Feature-Plans wird jeder Punkt **eingeschätzt** und
in einen **Detailplan** zerlegt. Aus dem Detailplan folgt, **welches Modell** ihn umsetzt.
Ziel: Teure Modelle denken und prüfen, günstige Modelle führen genau beschriebene Arbeit aus.
Vorlage: [docs/plan/detailplan-vorlage.md](docs/plan/detailplan-vorlage.md).

### 13.1 Einschätzen
Die steuernde Sitzung bewertet jeden Aufgabenpunkt (Checkbox der Feature-Datei) nach:

| Kriterium | Frage |
|---|---|
| **Umfang** | Wie viele Dateien und Zeilen ändern sich ungefähr? |
| **Risiko** | Datenmodell/Migration, Sync, Sicherheit/Auth, KI-Kosten, Lebensmittelsicherheit, Datenverlust? |
| **Neuheit** | Gibt es ein Muster im Code, dem man folgen kann, oder ist es neues Terrain? |
| **Klarheit** | Ist alles entschieden, oder sind noch Designfragen offen? |
| **Prüfbarkeit** | Belegen Build und Tests das Ergebnis, oder braucht es Messung bzw. Sichtprüfung? |

Ergebnis je Punkt: **Stufe 1–4** (siehe 13.3). Offene Designfragen werden **vor** dem Detailplan
geklärt (Grilling, Nutzer), nie vom ausführenden Modell nebenbei entschieden.

### 13.2 Detailplan
- Steht in der Feature-Datei im Abschnitt **„Detailplan“** (bei mehr als etwa 15 Dateien als
  eigene Datei `docs/plan/features/<id>-detail.md`).
- Je Arbeitspaket eine Tabelle mit **jeder** Datei:
  `Datei | neu/ändern | Was genau | Tests | ca. Zeilen`.
  „Was genau“ nennt Typen, Methoden mit Signatur, betroffene Stellen (Methode oder Zeilen) und
  das Muster, dem gefolgt wird (z. B. „wie `ExpiryCalculator`“).
- Dazu je Paket: Reihenfolge, Event-IDs und Telemetrie-Namen, Akzeptanz (welcher Test, welcher
  Befehl beweist es), **Modell** und **Effort**.
- Der Detailplan ist so genau, dass ein Modell der zugewiesenen Stufe **ohne eigene
  Designentscheidung und ohne breite Codesuche** umsetzen kann. Das spart die meisten Tokens:
  Der Planer liest den Code einmal, die Ausführenden lesen nur ihre Dateien.
- Pakete gleicher Stufe, die dieselben Dateien berühren, werden **ein** Paket. Mehrere kleine
  Pakete gleicher Stufe kommen in **einen** Subagenten (jeder Start kostet Kontext).
- Arbeit, die nur wenige Werkzeugaufrufe braucht, erledigt die steuernde Sitzung selbst.

### 13.3 Modellwahl
Stand 2026-11, Kosten grob relativ: Haiku ≈ 1/20 Sonnet, Sonnet ≈ ½ Opus, Opus ≈ ½ Fable.

| Stufe | Modell | Effort | Typische Arbeit |
|---|---|---|---|
| 1 – mechanisch | **Haiku** | `low` | Code suchen und zusammenfassen (Explore), Texte DE/EN, Telemetrie-Katalog, KB- und Plan-Häkchen, Log-Definitionen nach Vorgabe, Umbenennen, Datei aufteilen nach genauer Vorgabe |
| 2 – Standard nach Plan | **Sonnet** | `medium` | Domain-Logik mit Tests nach Detailplan, Settings/Mapping/DTOs, CRUD und UI-Komponenten nach bestehendem Muster, Build- und Testfehler beheben |
| 3 – anspruchsvoll | **Opus** | `high` | Detailplan erstellen, Datenmodell und Migrationen, Sync/Offline, Auth/Sicherheit, KI-Integration und Prompts, Haltbarkeitsregeln, unklare Fehlersuche, Integration und Merge-Konflikte, **jedes Review nach §9** |
| 4 – Grenzfall | **Fable** | `high` | Spikes mit offenem Ausgang, ADR-Entwürfe mit großer Tragweite (z. B. Stack, Sync-Modell), Probleme, an denen Opus gescheitert ist |

- **Steuernde Sitzung** und **Reviews** laufen mindestens auf Opus.
- Subagenten bekommen `model` und `effort` aus dem Detailplan (Agent-Tool: `model`, `effort`);
  Spuren nach §11 übernehmen das Modell ihres anspruchsvollsten Pakets oder werden in Pakete
  verschiedener Stufen geteilt.
- **Eskalation:** Scheitert ein Modell zweimal am selben Paket (Build oder Tests rot) oder stößt es
  auf eine Entscheidung, die der Plan nicht trifft, hört es auf und meldet das. Die steuernde
  Sitzung ergänzt den Plan oder vergibt das Paket eine Stufe höher. Kein Raten, kein Umgehen.
- **Rückblick:** Nach dem Meilenstein trägt die steuernde Sitzung in der Meilensteindatei ein,
  welche Einstufungen zu niedrig oder zu hoch waren, damit spätere Pläne besser schätzen.

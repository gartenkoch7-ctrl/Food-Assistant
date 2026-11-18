# M1 N – Backup und Haushaltsexport

Automatisches Datenbank-Backup täglich und **vor Migrationen** (ADR-0007, TODO in
`DatabaseMigrationService`), Aufbewahrung N Stück; Haushaltsexport/-import als versioniertes JSON
für Umzug und Datensicherung des Haushalts (`docs/kb/export-schema.md`).

Branch `feat/m01-n-backup-export` · M1b · Pakete: N1 und N2 unabhängig (nacheinander im Branch).
Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] N1 DB-Backup (pg_dump), Zeitplan, vor Migration, Aufbewahrung, Admin-Endpunkte, Doku Wiederherstellung
- [ ] N2 Haushaltsexport (JSON v1) und Import in neuen Haushalt, Schema-Doku, Seite

## Detailplan

### Paket N1 – DB-Backup

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** F11 (Plan nimmt a an)
- **Muster:** Optionen wie `Server/Api/Hosting/HostingOptions.cs` + `HostingOptionsValidator.cs`;
  Prozessaufruf als Dienst hinter Interface (AGENTS.md 2.6); `Infrastructure/Persistence/DatabaseMigrationService.cs`.

**Regeln:** `pg_dump --format=custom --no-owner --file=/backups/foodassistant-<yyyyMMdd-HHmmss>-<grund>.dump`
mit Verbindungsdaten aus dem Connection-String (`NpgsqlConnectionStringBuilder`), Passwort **nur**
über Umgebungsvariable `PGPASSWORD` des Kindprozesses (nie Argument, nie Log). Gründe: `daily`,
`pre-migration`, `manual`. Aufbewahrung: die neuesten `Keep` (Standard 14) Dateien je Gesamtbestand,
`pre-migration`-Dateien zählen mit. Vor Migration: nur wenn schon Migrationen angewendet sind
(nicht auf leerer DB); Backup scheitert → Server startet **nicht**, Meldung nennt Ursache und
`FOODASSISTANT__BACKUP__ALLOWMIGRATIONWITHOUTBACKUP=true` als bewussten Ausweg. In `Development`
ist Backup standardmäßig aus (`appsettings.Development.json`), weil `pg_dump` lokal fehlen kann.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Dockerfile` | ändern | Final-Stage: PGDG-Repo + `postgresql-client-17` (`apt-get … --no-install-recommends`, Listen löschen); Kommentar zur Emulation bei arm64 | Docker-Build lokal | +12 |
| `src/FoodAssistant.Application/Backup/IDatabaseBackup.cs` + `BackupInfo.cs` + `BackupReason.cs` | neu | `Task<BackupInfo> CreateAsync(BackupReason reason, CancellationToken)`; `Task<IReadOnlyList<BackupInfo>> ListAsync(…)`; `BackupInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAt, BackupReason Reason)` | – | 20+10+10 |
| `src/FoodAssistant.Infrastructure/Backup/BackupOptions.cs` + Validator | neu | `FoodAssistant:Backup`: `Enabled` (true), `Path` (`/backups`), `Keep` (14, 1–365), `DailyAt` (`03:00`, Serverzeit `TZ`), `AllowMigrationWithoutBackup` (false) | `BackupOptionsTests` | 40+30 |
| `src/FoodAssistant.Infrastructure/Backup/IProcessRunner.cs` + `ProcessRunner.cs` | neu | `Task<ProcessResult> RunAsync(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string,string> env, TimeSpan timeout, CancellationToken)` (`ArgumentList`, kein Shell) | – | 15+60 |
| `src/FoodAssistant.Infrastructure/Backup/PgDumpBackup.cs` | neu | implementiert `IDatabaseBackup`; Aufbewahrung; Fehler → `InvalidOperationException` mit Exit-Code (stderr gekürzt, ohne Passwort) | `PgDumpBackupTests` mit Fake-`IProcessRunner` (Argumente, `PGPASSWORD` nur in env, Aufbewahrung, Fehler) + ein Testcontainers-Test, der `pg_dump` **im** Postgres-Container ausführt (`ExecAsync`) – übersprungen, wenn nicht möglich | 150 |
| `src/FoodAssistant.Infrastructure/Backup/BackupJob.cs` | neu | täglicher Lauf zu `DailyAt` | `BackupJobTests` (Zeitberechnung) | 60 |
| `src/FoodAssistant.Infrastructure/Persistence/DatabaseMigrationService.cs` | ändern | TODO ersetzen: bei `pending > 0` und `applied > 0` → `IDatabaseBackup.CreateAsync(PreMigration)`; Dateiname in Fehlermeldung bei Migrationsfehler | `MigrationTests` erweitern (Fake-Backup) | +25 |
| `src/FoodAssistant.Infrastructure/Backup/BackupLog.cs` | neu | `BackupCreated` (2900, Information, `Reason`, `SizeBytes`, `ElapsedMilliseconds`), `BackupFailed` (2901, Error, `Reason`, `ExitCode`), `BackupPruned` (2902, Information, `Count`) – keine Pfade | – | 30 |
| Registrierungen (Stubs Backup) | ändern | | – | +5 |
| `src/FoodAssistant.Server/Backup/BackupEndpoints.cs` | ändern (Stub) | `/api/v1/admin/backups`: `GET` (ServerAdmin), `POST` (ServerAdmin, `WithoutIdempotency`) | `BackupEndpointTests` (nur Admin, Fake-Backup) | 50+100 |
| `docker-compose.yml`, `.env.example`, `deploy/README.md` | ändern | Variablen `FOODASSISTANT__BACKUP__*`; **Wiederherstellung**: `docker compose stop server`, `docker compose exec db pg_restore --clean --if-exists -U foodassistant -d foodassistant /backups/…` (Volume auch im db-Container einhängen), Server starten; Hinweis: Data-Protection-Volume mitsichern | – | +40 |
| `docs/kb/datenschutz.md`, `docs/kb/telemetrie.md` | ändern | Backups enthalten alles; Löschungen erst nach Rotation weg | – | +3 |

### Paket N2 – Haushaltsexport und -import

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Akzeptanz M1a (alle Datenarten bekannt) · **Blockiert durch:** F12 (Plan nimmt a an)
- **Muster:** Streaming-JSON mit `Utf8JsonWriter`; Stores wie A1.

**Schema v1** (`docs/kb/export-schema.md` anlegen): Wurzel `{ "format": "foodassistant-household", "schemaVersion": 1, "exportedAt", "appVersion", "household": { name, timeZone, expiryLeadDays }, "storageLocations": [...], "products": [... inkl. barcodes, shelfLifeOverrides], "categoryShelfLifeOverrides": [...], "batches": [...], "consumptionEvents": [... ohne Person], "shoppingLists": [... mit offenen items], "stores": [... mit sectionOrder, transitions], "recurringRules": [...] }`.
Ids bleiben im Export (Referenzen); Import vergibt **neue** Ids (Zuordnungstabelle), damit derselbe
Export mehrfach auf einem Server importiert werden kann. Keine Konten, Mitgliedschaften,
Einladungen, Verlauf, Benachrichtigungseinstellungen, Push-Abos (personenbezogen, F12 a).
**Regel für spätere Features:** neue Haushaltsdaten → neues Feld im Schema, `schemaVersion` nur bei Bruch erhöhen.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Export/HouseholdExportV1.cs` (+ je ein Record pro Abschnitt) | neu | Records des Schemas (eigene Export-Records, **nicht** die API-DTOs – Schema bleibt stabil, wenn sich die API ändert) | – | ~10×20 |
| `src/FoodAssistant.Application/Export/IHouseholdExportStore.cs` + `HouseholdExportService.cs` | neu | `ExportAsync(Guid householdId, Stream target, …)` (Owner); `ImportAsync(Stream source, Guid importingUserId, …)` → neue `HouseholdId` (Server-Admin): Validierung (Format, Version, Referenzen, Kategorien, Einheiten, Größe ≤ 50 MiB), neue Ids, eine Transaktion, Importierender wird Besitzer | `HouseholdExportServiceTests` | 25+200 |
| `src/FoodAssistant.Application/Export/ExportLog.cs` | neu | `HouseholdExported` (3850, Information, `HouseholdId`, Zählwerte), `HouseholdImported` (3851, `HouseholdId`), `ImportRejected` (3852, `Reason`) | – | 25 |
| `src/FoodAssistant.Infrastructure/Export/HouseholdExportStore.cs` + `HouseholdImportWriter.cs` | neu | Lesen je Abschnitt (`AsNoTracking`, Haushaltsfilter), Schreiben im Scope des **neuen** Haushalts über `IHouseholdScopeFactory` (Haushalt zuerst anlegen, dann Scope öffnen – kein `IgnoreQueryFilters`) | `ExportRoundTripTests` (Testcontainers): Haushalt mit allen Datenarten exportieren → importieren → zweiter Export inhaltsgleich bis auf Ids/Zeitpunkte | 200+200 |
| Registrierungen (Stubs Export) | ändern | | – | +3 |
| `src/FoodAssistant.Server/Export/ExportEndpoints.cs` | ändern (Stub) | `GET /export` (Owner) → `application/json`, `Content-Disposition: attachment; filename=haushalt-<datum>.json` | – | 40 |
| `src/FoodAssistant.Server/Households/HouseholdImportEndpoints.cs` | ändern (Stub) | `POST /households/import` (ServerAdmin, Upload ≤ 50 MiB, mit Idempotency-Key wie alle Schreibzugriffe – die Antwort ist klein) → 201 `HouseholdResponse` | `HouseholdExportEndpointTests` (Owner-only Export, Mitglied 403, Import nur Admin, kaputte Datei 400 mit Code `export.invalid_file`, falsche Version `export.unsupported_version`) | 50+200 |
| `src/FoodAssistant.Client/Export/*`, Registrierung | neu | Export als Stream, Import-Upload | – | 60 |
| `src/FoodAssistant.UI/Export/ExportPage.razor` + `ExportText.cs` + resx | neu | `@page "/settings/export"`: Besitzer → „Haushalt exportieren“ (Download über `download.js` aus J oder eigenem Modul); Server-Admin → „Haushalt importieren“ (Datei, Ergebnis mit Wechsel zum neuen Haushalt) und Backup-Liste (N1) mit „Jetzt sichern“ | `ExportPageTests` | 180+5+2×40 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Export & Backup“ | – | +5 |
| `docs/kb/export-schema.md` (neu, in README verlinken), `docs/kb/datenschutz.md`, `docs/kb/telemetrie.md`, `openapi-v1.json` | neu/ändern | Schema v1 mit Beispiel; was nicht enthalten ist und warum | – | – |

**Akzeptanz:** Tests grün inkl. Rundreise; Sichtprüfung: Export herunterladen, als Admin importieren, Daten vollständig.

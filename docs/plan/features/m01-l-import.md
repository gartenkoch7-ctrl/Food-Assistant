# M1 L – Import (CSV, Grocy)

Produkte und Vorrat aus einer CSV-Datei oder aus einer Grocy-Instanz übernehmen – jeweils mit
**Vorschau** (Zählungen, Fehler je Zeile, Zuordnungen) und erst danach **Übernehmen**. Kein
Zusammenführen bestehender Chargen; Produkte werden über Barcode oder exakten Namen wiedererkannt.
KB: [produkt-und-scope](../../kb/produkt-und-scope.md) „Import“, [datenschutz](../../kb/datenschutz.md).

Branch `feat/m01-l-import` · M1b · Pakete: L1 → L2. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] L1 CSV-Import (Format, Parser, Vorschau, Übernahme, Seite)
- [ ] L2 Grocy-Import (Client, Zuordnung, Vorschau, Übernahme, Reiter auf der Seite)

## Gemeinsame Regeln
- Nur `HouseholdMember`. Vorschau liegt **im Speicher des Servers** (`IMemoryCache`, 30 min,
  Schlüssel `(HouseholdId, ImportId)`), nicht in der Datenbank; Übernahme mit `ImportId` und
  Idempotency-Key. Neustart → Vorschau weg → 404 `import.preview_expired`.
- Übernahme in **einer** Transaktion: fehlende Lagerorte und Produkte anlegen, Chargen mit
  Herkunft `Import`; danach `IStockChangedHandler`. Ereignisse/Verlauf: eine Aktivität `import`.
- Grenzen: Datei ≤ 2 MiB, ≤ 5000 Zeilen; Grocy-Antworten zusammen ≤ 20 MiB.

## Detailplan

### Paket L1 – CSV

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** –
- **Muster:** Domain-Parser rein (wie `FefoPlanner.cs`); Upload als `IFormFile` in Minimal-API (`.DisableAntiforgery()` **nicht** – Cookie-Antiforgery-Filter wie andere Haushaltsendpunkte).

**CSV-Format v1** (Doku als `docs/kb/import.md`): UTF-8 mit/ohne BOM, Trennzeichen `;` oder `,`
(aus der Kopfzeile erkannt), RFC-4180-Anführungszeichen. Kopfzeile Pflicht, Spalten (Reihenfolge egal):
`name` (Pflicht), `brand`, `category` (Schlüssel aus F1 oder DE/EN-Name), `barcode`, `unit`
(Code, Standard `piece`), `minimum_stock`, `location`, `location_type` (`chilled|frozen|ambient`),
`quantity`, `expires_on` (`yyyy-MM-dd` oder `dd.MM.yyyy`), `expiry_kind` (`best_before|use_by`).
Zeile mit `quantity` → Charge (Ort Pflicht); ohne → nur Produkt. Dezimaltrenner `,` oder `.`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/Import/CsvReader.cs` | neu | `static IReadOnlyList<IReadOnlyList<string>> Parse(string text, out char delimiter)` (RFC 4180, Zeilenumbrüche in Anführungszeichen) | `CsvReaderTests` | 100 |
| `src/FoodAssistant.Application/Import/CsvInventoryMapper.cs` + `ImportRow.cs` + `ImportPlan.cs` | neu | Zeilen → `ImportPlan` (Produkte, Orte, Chargen) + `ImportRowError(int Line, string Column, string Code)` | `CsvInventoryMapperTests` (alle Spalten, Fehlerfälle, Kategorie per Name) | 160+20+30 |
| `src/FoodAssistant.Application/Import/IImportStore.cs` + `ImportService.cs` + `ImportPreviewCache.cs` + `ImportLog.cs` | neu | Vorschau (Abgleich mit bestehenden Produkten/Orten, Zählung neu/vorhanden), Übernahme; Log `ImportCommitted` (3750, Information, `Source`, `Products`, `Batches`), `ImportRejected` (3751, `Reason`) | `ImportServiceTests` | 30+180+50+20 |
| `src/FoodAssistant.Infrastructure/Import/ImportStore.cs` | neu | EF, eine Transaktion, `StockBatchMapping.NewRecord` | `ImportStoreTests` | 160 |
| `src/FoodAssistant.Contracts/Import/*` | neu | `ImportPreviewDto(Guid ImportId, int NewProducts, int ExistingProducts, int NewLocations, int Batches, IReadOnlyList<ImportRowErrorDto> Errors, IReadOnlyList<ImportLocationMappingDto> Locations)`, `ImportCommitRequest(Guid ImportId, IReadOnlyList<ImportLocationMappingDto>? Locations)`, `ImportResultDto`, `ImportErrorCodes` | – | 5×12 |
| Registrierungen (Stubs Import) | ändern | inkl. `AddMemoryCache()` | – | +4 |
| `src/FoodAssistant.Server/Import/ImportEndpoints.cs` | ändern (Stub) | `POST /import/csv/preview` (multipart, Feld `file`, Größenlimit), `POST /import/{importId:guid}/commit` | `CsvImportEndpointTests` (Vorschau, Übernahme, abgelaufene Vorschau 404, Wiederholung idempotent, Eingeschränkt 403, fremder Haushalt) | 70+200 |
| `src/FoodAssistant.Client/Import/*`, Registrierung | neu | Upload (`MultipartFormDataContent`) über `ApiHttpClient` (neue Methode `SendMultipartAsync` – kleine Erweiterung in S0-Datei, nach M1a frei) | – | 70+20 |
| `src/FoodAssistant.UI/Import/ImportPage.razor` + `CsvImportPanel.razor` + `ImportText.cs` + resx | neu | `@page "/import"`: Reiter CSV/Grocy; CSV: Datei wählen (`InputFile`), Vorschau (Zahlen, Fehlerliste mit Zeile/Spalte), Lagerort-Typen für neue Orte, „Übernehmen“; Beispieldatei-Link (`wwwroot/import-beispiel.csv`) | `ImportPageTests`, `CsvImportPanelTests` | 80+170+5+2×50 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Import“ | – | +5 |
| `docs/kb/import.md` (neu, in `docs/kb/README.md` verlinken), `docs/kb/telemetrie.md`, `openapi-v1.json` | neu/ändern | Format v1; Telemetrie | – | – |

### Paket L2 – Grocy

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** L1 · **Blockiert durch:** F10
- **Muster:** typisierter HttpClient wie `Infrastructure/FoodFacts/OpenFoodFactsApi.cs` (C1).

**Grocy-API (Header `GROCY-API-KEY`):** `GET /api/objects/products`, `/api/objects/locations`
(`is_freezer`), `/api/objects/quantity_units`, `/api/objects/product_barcodes`,
`/api/objects/stock` (Einträge: `product_id`, `amount`, `best_before_date`, `location_id`, `open`),
`/api/objects/shopping_lists`, `/api/objects/shopping_list` (`shopping_list_id`, `product_id`, `note`, `amount`, `done`).
`best_before_date = 2999-12-31` → kein Datum; Produkt `due_type` 1 = MHD, 2 = Verbrauchsdatum.
`open = 1` → Charge als geöffnet (Datum wie geliefert, Zustand `Opened`, `OpenedOn` = Importtag –
Hinweis in der Vorschau). Mengeneinheiten: Abgleich über Namen (g, Gramm, kg, ml, l, Liter, Stück,
Packung, Dose, Bund; groß/klein egal) → Code; sonst Nutzer wählt in der Vorschau.
Lagerorte: `is_freezer` → `Frozen`, sonst Vorschlag `Chilled`, Nutzer kann ändern.
Einkaufslisten: offene Einträge (`done = 0`) auf gleichnamige Listen (neu angelegt), Herkunft `Import`.

**Sicherheit (F10 a):** nur http/https, keine Weiterleitungen (`AllowAutoRedirect = false`), Zeitlimit
30 s gesamt, Antwortgröße begrenzt, nur die festen Pfade, API-Key nur im Request (nie gespeichert,
nicht in der Vorschau, nie geloggt), Basis-URL nicht geloggt. Nur `HouseholdOwner`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/Import/IGrocySource.cs` + `GrocySnapshot.cs` | neu | `Task<GrocyFetchResult> FetchAsync(Uri baseUrl, string apiKey, CancellationToken)`; Snapshot-Records (nur benötigte Felder) | – | 20+50 |
| `src/FoodAssistant.Application/Import/GrocyMapper.cs` | neu | Snapshot + Zuordnungen → `ImportPlan` (inkl. Listen) | `GrocyMapperTests` (Einheiten, `2999-12-31`, `due_type`, offen, Freezer) | 180 |
| `src/FoodAssistant.Application/Import/ImportService.cs` | ändern | `PreviewGrocyAsync(…)`, Übernahme nutzt denselben Weg | `ImportServiceTests` erweitern | +70 |
| `src/FoodAssistant.Infrastructure/Import/GrocyClient.cs` + `GrocyOptions.cs` | neu | Regeln oben; Fehlerarten `unreachable`, `unauthorized`, `invalid_response`, `too_large` | `GrocyClientTests` (Fake-Handler mit selbst erstellten JSON-Antworten; Weiterleitung wird nicht verfolgt; Größenlimit) | 160+30 |
| `src/FoodAssistant.Contracts/Import/GrocyPreviewRequest.cs` + `ImportUnitMappingDto.cs` | neu | `GrocyPreviewRequest(string BaseUrl, string ApiKey)`; Zuordnungen in Vorschau/Übernahme | – | 2×10 |
| `src/FoodAssistant.Server/Import/ImportEndpoints.cs` | ändern | `POST /import/grocy/preview` (Owner) | `GrocyImportEndpointTests` (Fake-`IGrocySource`, Owner-only, Fehlerabbildung) | +30+150 |
| `src/FoodAssistant.UI/Import/GrocyImportPanel.razor` | neu | URL, API-Key (Passwortfeld, `autocomplete="off"`), Vorschau mit Zuordnung Orte/Einheiten, Übernehmen; Hinweis, dass der Key nicht gespeichert wird | `GrocyImportPanelTests` | 200 |
| `docs/kb/datenschutz.md`, `docs/kb/externe-daten.md`, `docs/kb/import.md` | ändern | Grocy: was abgerufen wird, Key nie gespeichert, SSRF-Regeln | – | +10 |

**Akzeptanz:** Tests grün; Sichtprüfung CSV mit Beispieldatei; Grocy gegen eine lokale Grocy-Demo
(Docker `linuxserver/grocy`, nur wenn der Nutzer es erlaubt) oder nur mit Fakes.

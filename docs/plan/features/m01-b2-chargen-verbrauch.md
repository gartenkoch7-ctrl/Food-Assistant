# M1 B2 – Chargen, Zustände und Verbrauch

Chargen einbuchen, korrigieren, verschieben (inkl. Einfrieren/Auftauen über den Lagerort-Typ),
öffnen, Rest anlegen; verbrauchen (aufgebraucht, teilweise, entsorgt mit Grund), Verbrauch eines
Produkts nach FEFO; Verbrauchsereignisse; Rückgängig (F4, Annahme a); Vorratsübersicht.
Sicherheitsrelevant (Ablaufdaten). KB: [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md), ADR-0010.

Branch `feat/m01-b2-stock` · Pakete nacheinander: B2.1 → B2.2 → B2.3 (B2.3 erst nach Merge A).

## Aufgaben
- [x] B2.1 Domain: Verschieben, Korrektur
- [ ] B2.2 Server: Chargen, Verbrauch, FEFO, Ereignisse, Rückgängig, Übersicht
- [ ] B2.3 Client und UI: Vorratsübersicht, Produkt-Chargen, Dialoge, Chargen-Seite

## Fachregeln (verbindlich)

- **Neue Charge** (`AddBatch`): Lagertyp = Typ des Ziel-Lagerorts.
  - Nicht gefroren: `new StockBatch(id, qty, type, packageDate, kind)`.
  - **Gefroren (F5, Annahme a):** mit Packungsdatum und `SelfFrozen = false` → gekaufte TK-Ware:
    `new StockBatch(…, StorageType.Frozen, packageDate, kind)` (verschlossen, Datum gilt).
    Ohne Packungsdatum **oder** `SelfFrozen = true` → `new StockBatch(…, StorageType.Chilled, packageDate, kind)`
    und sofort `Freeze(today, rule(Kategorie, Frozen))`.
  - Bis F5 beantwortet ist, ist nur der Zweig „gefroren“ blockiert; Test dafür mit `Skip`-Grund „F5“.
- **Verschieben** auf anderen Lagerort: `BatchMover.Move(batch, targetType, today, rule)` (B2.1);
  Regel je nach Übergang (siehe B1 „Welche Zeile gilt wann“). Aufgetaut → gefroren ist verboten
  (Domain wirft; API 422 `inventory.refreeze_not_allowed`).
- **Öffnen:** nur verschlossen (sonst 422 `inventory.invalid_state`), Regel (Kategorie, aktueller Typ).
- **Rest anlegen:** `StockBatch.CreateLeftover(id, qty, typeOfLocation, today, rule)`, Herkunft `Leftover`.
- **Korrektur (F14, Annahme a):** Menge immer; Packungsdatum/Art nur bei `Sealed` (sonst 422
  `inventory.date_locked`).
- **Verbrauch:** `UsedUp` = ganze Restmenge; `Partial` = Menge > 0 und < Rest (sonst 400 Feldfehler);
  `Discarded` = Menge optional (Standard: Rest), Grund Pflicht. Menge muss in die Einheit der
  Charge umrechenbar sein. Leere Charge wird **gelöscht** (Tombstone über ChangeLog).
- **Produkt verbrauchen (FEFO):** `FefoPlanner.Plan(batches, amount)`; ohne Menge bei `UsedUp` =
  ganze früheste Charge. Fehlmenge (`Shortfall`) → 422 `inventory.not_enough_stock` mit verfügbarer Menge.
- **Ereignis** je betroffener Charge: `ConsumptionEvent` mit `ExpiryStatusAtEvent` (aus
  `ExpiryEvaluator` mit Haushalts-„heute“ und Vorlauf), `OccurredOn` = Haushaltsdatum, `UserId`.
- **Verlauf/Rückgängig:** jede Aktion schreibt `IActivityLog.Add` (Area `Inventory`, Kind aus
  `InventoryActivityKinds`, `PayloadJson` = `BatchSnapshotJson` mit Snapshot **vorher**, Lagerort,
  Ereignis-Id und dem `UpdatedAt`, das die Charge **nach** der Aktion trägt). `SubjectVersionAfter`
  bleibt null (xmin steht erst nach dem Speichern fest; geprüft wird über `UpdatedAt`).
  Rückgängig erlaubt, wenn: Aktivität ≤ 24 h alt, nicht schon rückgängig, und die Charge seitdem
  unverändert (`UpdatedAt` der Charge == gespeicherter Wert) bzw. noch gelöscht, wenn die Aktion sie
  gelöscht hat. Rückgängig stellt den Snapshot wieder her (gelöschte Charge mit gleicher Id neu),
  setzt `ConsumptionEvent.UndoneAt` und `HouseholdActivity.UndoneAt` und schreibt eine Aktivität
  `undone`. Sonst 409 `inventory.undo_not_possible`.
- **Bestands-Hook:** nach jedem erfolgreichen Speichern, das Menge ändert/Chargen anlegt/löscht,
  alle `IStockChangedHandler` mit den Produkt-Ids aufrufen (Fehler loggen, nicht weiterreichen).
- **Rechte:** Lesen `HouseholdAny`, alles andere `HouseholdMember`.
- **Status im DTO:** `ExpiryEvaluator.Evaluate(ExpiresOn, today, household.ExpiryLeadDays)`.

## Detailplan

### Paket B2.1 – Domain

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Blockiert durch:** – (F14 Annahme a)
- **Muster:** `src/FoodAssistant.Domain/Inventory/StockBatch.cs` (bestehende `Freeze`/`Thaw`, `EnsureState`),
  Tests `tests/FoodAssistant.Domain.Tests/Inventory/StockBatchFreezeThawTests.cs` + `TestBatches.cs`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Inventory/BatchMove.cs` | neu | `enum BatchMove { SameType, Frozen, Thawed, Relocated }` | – | 12 |
| `src/FoodAssistant.Domain/Inventory/StockBatch.cs` | ändern | nur zwei kleine Methoden (Datei hat nach S0.1 ≤ 250 Zeilen, danach ≤ 285): `internal void Relocate(StorageType target)` (wirft, wenn `target` oder aktueller Typ `Frozen` ist – dafür gibt es Freeze/Thaw); `public void Correct(Quantity quantity, DateOnly? packageExpiresOn, ExpiryKind packageExpiryKind)`: Menge > 0 und gleiche Dimension; Datum/Art nur änderbar, wenn `State == Sealed` (sonst bei Abweichung `InvalidOperationException`); bei Sealed `ExpiresOn/ExpiryKind` = Packungswerte | `StockBatchCorrectTests` | +35 |
| `src/FoodAssistant.Domain/Inventory/BatchMover.cs` | neu | `public static BatchMove Move(StockBatch batch, StorageType target, DateOnly today, ShelfLifeRule rule)`: gleich → `SameType`; Ziel `Frozen` → `batch.Freeze` (wirft bei aufgetaut) → `Frozen`; von `Frozen` → `batch.Thaw(today, rule, target)` → `Thawed`; sonst `batch.Relocate(target)` → `Relocated` | `BatchMoverTests` (alle Übergänge, aufgetaut→gefroren wirft) | 45 |

**Akzeptanz:** Domain-Tests grün, Abdeckung neuer Methoden 100 %.

### Paket B2.2 – Server

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** B2.1 · **Blockiert durch:** F5 nur Zweig „neue Charge im Gefrierschrank“
- **Muster:** wie A1; Store-Muster `docs/kb/datenmodell.md` (S0.2); `StockBatchMapping` (S0.1);
  `IHouseholdCalendar`, `IShelfLifeRules`, `IActivityLog`, `IStockChangedHandler` (S0.2).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Inventory/BatchDto.cs` | neu | `(Guid Id, Guid ProductId, Guid StorageLocationId, QuantityDto Quantity, StorageTypeDto StorageType, BatchStateDto State, DateOnly? ExpiresOn, ExpiryKindDto ExpiryKind, DateOnly? PackageExpiresOn, ExpiryKindDto PackageExpiryKind, DateOnly? OpenedOn, DateOnly? FrozenOn, DateOnly? ThawedOn, DateOnly? PreparedOn, BatchOriginDto Origin, ExpiryStatusDto Status, int? DaysUntilExpiry, long Version)` | – | 25 |
| `src/FoodAssistant.Contracts/Inventory/InventoryProductDto.cs` | neu | `(ProductSummaryDto Product, IReadOnlyList<QuantityDto> Totals, ExpiryStatusDto WorstStatus, DateOnly? NextExpiry, IReadOnlyList<BatchDto> Batches)` – `Totals` je nicht umrechenbarer Einheit eine Summe | – | 15 |
| `src/FoodAssistant.Contracts/Inventory/AddBatchRequest.cs`, `CorrectBatchRequest.cs`, `MoveBatchRequest.cs`, `ConsumeBatchRequest.cs`, `ConsumeProductRequest.cs`, `CreateLeftoverRequest.cs`, `InventoryActionResponse.cs`, `InventoryErrorCodes.cs` | neu | `AddBatchRequest(Guid? Id, Guid ProductId, Guid StorageLocationId, QuantityDto Quantity, DateOnly? PackageExpiresOn, ExpiryKindDto PackageExpiryKind, BatchOriginDto Origin, bool SelfFrozen)`; `CorrectBatchRequest(QuantityDto Quantity, DateOnly? PackageExpiresOn, ExpiryKindDto PackageExpiryKind)`; `MoveBatchRequest(Guid StorageLocationId)`; `ConsumeBatchRequest(ConsumptionKindDto Kind, QuantityDto? Quantity, ConsumptionReasonDto? Reason)`; `ConsumeProductRequest(ConsumptionKindDto Kind, QuantityDto? Quantity, ConsumptionReasonDto? Reason)`; `CreateLeftoverRequest(Guid? Id, Guid ProductId, Guid StorageLocationId, QuantityDto Quantity)`; `InventoryActionResponse(IReadOnlyList<BatchDto> Batches, IReadOnlyList<Guid> RemovedBatchIds, Guid ActivityId)`; Codes `inventory.batch_not_found`, `inventory.validation_failed`, `inventory.invalid_state`, `inventory.refreeze_not_allowed`, `inventory.date_locked`, `inventory.not_enough_stock`, `inventory.undo_not_possible`, `inventory.unit_mismatch` | – | 8×15 |
| `src/FoodAssistant.Application/Inventory/InventoryActivityKinds.cs` | neu | Konstanten `batch_added`, `batch_corrected`, `batch_moved`, `batch_frozen`, `batch_thawed`, `batch_opened`, `leftover_created`, `used_up`, `consumed_partly`, `discarded`, `undone` | – | 20 |
| `src/FoodAssistant.Application/Inventory/IInventoryStore.cs` | neu | `ListAsync(InventoryFilter, …)` → Chargen + Produkt-Zusammenfassungen; `GetBatchAsync(Guid id, …)` → `BatchContext?` (`BatchContext(StockBatch Batch, Guid ProductId, string CategoryKey, Guid StorageLocationId, BatchOrigin Origin, DateTimeOffset UpdatedAt, long Version)` – Domain-Charge plus Kontext, keine EF-Entität nach außen); `GetBatchesOfProductAsync(Guid productId, …)`; `GetLocationAsync(Guid id, …)` → `(Guid Id, StorageType Type)?`; `GetProductCategoryAsync(Guid productId, …)`; `AddAsync(NewBatch, …)`; `SaveAsync(BatchChange change, …)` → `OperationResult<…>` (eine Transaktion für Charge(n), Ereignisse, Aktivität); `GetActivityAsync(Guid id, …)`; `RestoreAsync(UndoPlan, …)` | – | 50 |
| `src/FoodAssistant.Application/Inventory/{BatchContext,NewBatch,BatchChange,InventoryFilter,UndoPlan}.cs` | neu | Records für den Store (Felder laut Fachregeln) | – | 5×20 |
| `src/FoodAssistant.Application/Inventory/BatchDtoMapper.cs` | neu | `static BatchDto ToDto(BatchContext ctx, HouseholdToday today)`; `static InventoryProductDto ToProductDto(ProductSummaryDto, IEnumerable<BatchContext>, HouseholdToday)` (Summen je Dimension über `MinimumStockPolicy.TotalStock`, schlechtester Status: Expired < Soon < Ok < NoDate) – **öffentlich**, E2 nutzt es | `BatchDtoMapperTests` | 90 |
| `src/FoodAssistant.Application/Inventory/BatchSnapshotJson.cs` | neu | `static string Serialize(StockBatchSnapshot, BatchLocationInfo)`, `static (StockBatchSnapshot, BatchLocationInfo) Deserialize(string)` – eigenes primitives JSON-Format (Einheit als Code, Daten `yyyy-MM-dd`) | `BatchSnapshotJsonTests` | 80 |
| `src/FoodAssistant.Application/Inventory/InventoryQueryService.cs` | neu | `ListAsync(Guid householdId, InventoryQuery q, …)` → `OperationResult<CursorPage<InventoryProductDto>>`: Filter `status` (`expired`, `soon`, `all`), `locationId`, `query` (Name); Sortierung `NextExpiry` (null zuletzt), Name, Id; Cursor `InventoryCursor(DateOnly? NextExpiry, string Name, Guid ProductId)`; in-memory über alle Chargen des Haushalts (Kommentar: Haushalt hat wenige tausend Chargen; bei Bedarf später in SQL); `GetProductAsync`, `GetBatchAsync` | `InventoryQueryTests` (Server.Tests) | 140 |
| `src/FoodAssistant.Application/Inventory/BatchService.cs` | neu | `AddAsync`, `CorrectAsync(…, long? expectedVersion, …)`, `MoveAsync(…, long? expectedVersion, …)`, `OpenAsync(…, long? expectedVersion, …)`, `CreateLeftoverAsync` – Regeln oben; Domain-Ausnahmen (`InvalidOperationException`) → `RuleViolated` mit passendem Code; `Activity` + Log + Hook | über Endpunkt-Tests | 230 |
| `src/FoodAssistant.Application/Inventory/ConsumptionService.cs` | neu | `ConsumeBatchAsync(Guid householdId, Guid batchId, long? expectedVersion, ConsumeBatchRequest, …)`, `ConsumeProductAsync(Guid householdId, Guid productId, ConsumeProductRequest, …)` (FEFO), Ereignisse, Metrik | über Endpunkt-Tests | 200 |
| `src/FoodAssistant.Application/Inventory/UndoService.cs` | neu | `UndoAsync(Guid householdId, Guid activityId, Guid userId, …)` – Regeln oben | über Endpunkt-Tests | 120 |
| `src/FoodAssistant.Application/Inventory/InventoryLog.cs` | neu | `BatchAdded` (3300, Information, `BatchId`, `ProductId`, `Origin`), `BatchStateChanged` (3301, Information, `BatchId`, `Change`), `BatchConsumed` (3302, Information, `BatchId`, `Kind`), `UndoApplied` (3303, Information, `ActivityId`), `UndoRejected` (3304, Information, `Reason`), `StockHandlerFailed` (3305, Warning, Exception) | – | 40 |
| `src/FoodAssistant.Application/Inventory/InventoryMetrics.cs` | neu | `foodassistant.inventory.consumptions` (Counter `{event}`, `kind`, `reason`), `foodassistant.inventory.batches.added` (Counter `{batch}`, `origin`) über `Telemetry.Meter` | – | 30 |
| `src/FoodAssistant.Application/Composition/InventoryRegistration.cs` | ändern (Stub) | Services registrieren | – | +4 |
| `src/FoodAssistant.Infrastructure/Inventory/InventoryStore.cs` | neu | EF über `StockBatchRecord`, `ConsumptionEvent`, `HouseholdActivity`; nutzt `StockBatchMapping`; Speichern in **einer** `SaveChangesAsync` (Aktivität kommt über `IActivityLog` in denselben Context); Versionsprüfung nach Store-Muster | `InventoryStoreTests` (Testcontainers): Löschen leerer Charge, Wiederherstellen mit gleicher Id, Haushaltsfilter | 260 |
| `src/FoodAssistant.Infrastructure/Composition/InventoryInfrastructureRegistration.cs` | ändern (Stub) | `AddScoped<IInventoryStore, InventoryStore>()` | – | +1 |
| `src/FoodAssistant.Server/Inventory/InventoryEndpoints.cs` | ändern (Stub) | Tag `Inventory`: `GET /inventory?status&locationId&query&cursor&limit`; `GET /inventory/products/{productId:guid}`; `GET /inventory/batches/{batchId:guid}`; `POST /inventory/batches` (201); `PUT /inventory/batches/{batchId:guid}` (If-Match Pflicht); `POST /inventory/batches/{batchId:guid}/move` (If-Match Pflicht); `POST …/open` (If-Match Pflicht); `POST …/consume` (If-Match optional); `POST /inventory/leftovers` (201); `POST /inventory/products/{productId:guid}/consume`; `POST /inventory/activities/{activityId:guid}/undo` | – | 190 |
| `tests/FoodAssistant.Server.Tests/Inventory/BatchEndpointTests.cs` | neu | Einbuchen (Kühlschrank, Vorratsschrank), Korrektur (Sealed ok, Opened → 422), Öffnen mit/ohne Regel (Fake-`IShelfLifeRules` über `ConfigureTestServices`), Einfrieren/Auftauen über Verschieben, aufgetaut→gefroren 422, ETag 412/428, Eingeschränkt 403, **fremder Haushalt 404** (Charge und Lagerort/Produkt eines anderen Haushalts) | ja | 280 |
| `tests/FoodAssistant.Server.Tests/Inventory/ConsumptionEndpointTests.cs` | neu | aufgebraucht/teilweise/entsorgt, Einheiten umrechnen (500 g von 1 kg), Fehlmenge 422, FEFO über drei Chargen (ohne Datum zuletzt), Ereignisse mit Status, Hook aufgerufen | ja | 240 |
| `tests/FoodAssistant.Server.Tests/Inventory/UndoEndpointTests.cs` | neu | Rückgängig nach Verbrauch (Charge wieder da, Ereignis `UndoneAt`), nach Öffnen, nach geänderter Charge 409, doppelt 409, älter 24 h 409 (`MutableTimeProvider`) | ja | 180 |
| `tests/FoodAssistant.Server.Tests/Inventory/InventoryQueryTests.cs` | neu | Sortierung, Filter, Cursor, Status mit Haushalts-Zeitzone und Vorlauf | ja | 160 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md` | ändern | Snapshot; Abläufe `foodassistant.inventory.add_batch`, `.correct_batch`, `.move_batch`, `.open_batch`, `.create_leftover`, `.consume_batch`, `.consume_product`, `.undo` (Attribute `foodassistant.outcome`, ggf. `foodassistant.batches` Anzahl) | – | – |

**Akzeptanz:** alle Tests grün; jeder Endpunkt mit Test für fremden Haushalt und Eingeschränkt;
Snapshot aktualisiert; `docs/kb/vorrat-und-haltbarkeit.md` um „Rückgängig (24 h)“ und „TK-Ware“ ergänzt.

### Paket B2.3 – Client und UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** B2.2 **und Merge A** (`ProductPicker`) · **Muster:** wie A2; `UI/Inventory/StatusChip.razor`.

**Ablauftexte** (KB „Status und Formulierungen“): abgelaufen „seit gestern abgelaufen“ / „seit {n}
Tagen abgelaufen“; bald „heute“, „morgen“, „noch {n} Tage“; ok „bis {d.M.}“ bzw. ab 7 Tagen nur
Datum; ohne „kein Datum“. Hinweis bei abgelaufenem MHD vs. Verbrauchsdatum wörtlich aus der KB;
bei Verbrauchsdatum Status im zugänglichen Namen.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Inventory/IInventoryClient.cs` + `InventoryClient.cs` | neu | Methoden zu allen B2.2-Endpunkten (`ApiResult<…>`, ETags) | `InventoryClientTests` | 40+150 |
| `src/FoodAssistant.Client/Composition/InventoryClientRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.UI/Inventory/ExpiryText.cs` | neu | `static string Describe(BatchDto b, IStringLocalizer<InventoryText> T, CultureInfo c)` und `Hint(...)` nach Tabelle oben | `ExpiryTextTests` (DE/EN, alle Fälle, Grenzen 6/7 Tage) | 80 |
| `src/FoodAssistant.UI/Inventory/InventoryPage.razor` | ändern (ersetzt Platzhalter) | `@page "/"`: Filter-Chips (Alle, Läuft bald ab, Abgelaufen), Lagerort-Auswahl, Suche; Liste der Produkte (`ListRow`: Kategorie-Symbol, Name, „2 Chargen · 1,5 l · Kühlschrank“, rechts `StatusChip` mit `ExpiryText`); Klick → `InventoryProductSheet`; FAB „Einbuchen“ → `AddBatchDialog`; leer → `EmptyState` (bestehend); Live: `Changed` mit `batch`/`product`/`storage_location` → neu laden, `Resynced` → neu laden; Ansage über `ILiveAnnouncer` nur bei fremden Änderungen | `InventoryPageTests` | 200 |
| `src/FoodAssistant.UI/Inventory/InventoryProductSheet.razor` | neu | `MudDialog` als Sheet: Chargen mit Ort, Menge, Zustand (Text), Ablauf; Aktionen je Charge (nur `SessionState.CanEdit`): Aufgebraucht, Teilweise…, Entsorgt…, Geöffnet, Verschieben…, Korrigieren…, Rest anlegen…; nach Aktion `IUndoNotifier.Show` mit `UndoAsync(activityId)` | `InventoryProductSheetTests` | 220 |
| `src/FoodAssistant.UI/Inventory/AddBatchDialog.razor` | neu | `ProductPicker` (oder vorgegebenes Produkt – Parameter `ProductSummaryDto? Product`, `QuantityDto? Quantity`, `string? Barcode` für C2), Lagerort, Menge (`QuantityInput`, Standardeinheit des Produkts), Datum (`DateOnlyField`) + Art (MHD/Verbrauchsdatum/keins, Pflicht wenn Datum), bei Gefrier-Lagerort Schalter „selbst eingefroren“; `Origin` als Parameter (Standard `Manual`) | `AddBatchDialogTests` | 170 |
| `src/FoodAssistant.UI/Inventory/ConsumeDialog.razor` | neu | Modus Teilweise (Menge) oder Entsorgt (Grund als Radiogruppe, Menge optional) | `ConsumeDialogTests` | 110 |
| `src/FoodAssistant.UI/Inventory/MoveBatchDialog.razor`, `CorrectBatchDialog.razor`, `LeftoverDialog.razor` | neu | Lagerort-Auswahl mit Hinweis „wird eingefroren/aufgetaut“; Korrektur (Datum gesperrt außer verschlossen, mit Erklärung); Rest (Produkt, Menge, Ort) | je Test | 3×90 |
| `src/FoodAssistant.UI/Inventory/BatchPage.razor` | neu | `@page "/inventory/batches/{BatchId:guid}"` (QR-Ziel der Etiketten): Charge mit Produkt, Aktionen wie im Sheet | `BatchPageTests` | 120 |
| `src/FoodAssistant.UI/Inventory/InventoryText*.resx` | ändern | alle neuen Texte, `Error.inventory.*` | `ResourceCompletenessTests` | +2×80 |
| `tests/FoodAssistant.UI.Tests/Inventory/*` + `Support/FakeInventoryClient.cs` | neu | bUnit | ja | 8×110+120 |

**Barrierefreiheit:** Checkliste; Status nie nur Farbe; Aktionen als beschriftete Knöpfe;
Fokus zurück nach Dialog; Rückgängig per Tastatur erreichbar (Snackbar-Aktion).
**Akzeptanz:** Tests grün; Sichtprüfung im Browser-Pane: einbuchen, öffnen, einfrieren, auftauen,
teilweise verbrauchen, entsorgen, Rückgängig.

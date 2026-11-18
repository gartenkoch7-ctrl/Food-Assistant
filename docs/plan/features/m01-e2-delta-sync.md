# M1 E2 – Delta-Sync und Aufbewahrung

`GET …/sync/changes?since=` für den Offline-Cache der App (ADR-0005): geänderte Entitäten als
vollständige DTOs plus Löschungen, neues Token; zu altes Token → volle Neuladung. Dazu ein
Aufbewahrungsdienst: Änderungsprotokoll und gelöschte Einkaufseinträge 30 Tage, Verlauf 90 Tage.

Branch `feat/m01-e2-delta-sync` · M1b · ein Paket. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] E2 Delta-Feed, Token, Aufbewahrung, Tests, KB

## Detailplan

### Paket E2 – Delta-Feed

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** –
- **Muster:** Mapper `Infrastructure/Catalog/CatalogMapping.cs` (A), `Application/Inventory/BatchDtoMapper.cs` (B2),
  `Application/Shopping/ShoppingMapping.cs` (D), `Store`-DTO aus D2; Cursor-Kodierung `Server/Api/Pagination/CursorPagination.cs`;
  Hintergrunddienst wie `Application/Restock/RecurringItemsJob.cs` (G2).

**Token:** `CursorPagination.Encode(new SyncToken(long Sequence))`. Ablauf:
- kein `since` → **Vollstand**: alle aktuellen Entitäten der sechs Typen (Lagerorte, Produkte nicht archiviert,
  Chargen, Listen, nicht gelöschte Einträge, Läden), `NextToken` = höchste `Sequence` des Haushalts (0 wenn keine).
- mit `since` → `ChangeLog` mit `Sequence > since` aufsteigend, max. `limit` (Standard 500, max 2000)
  **Zeilen**; je `(EntityType, EntityId)` nur der letzte Eintrag; aktuelle DTOs laden, `Deleted`
  bzw. heute nicht mehr vorhanden/archiviert/soft-gelöscht → in `Deleted`; `HasMore`, wenn das Limit erreicht wurde.
- `since < PrunedThroughSequence` des Haushalts (Tabelle `SyncRetention`, s. u.; fehlt = 0) → 410
  `sync.token_expired` (Client lädt den Vollstand).
- Ungültiges Token → 400 `common.cursor_invalid`.

**Aufbewahrung (täglich 03:30 Serverzeit, je Haushalt im Scope):** `ChangeLog` älter 30 Tage löschen und
die höchste gelöschte `Sequence` als `PrunedThroughSequence` merken (Tabelle `SyncRetention`:
`HouseholdId` PK, `PrunedThroughSequence long`) → **Schemaänderung**: Migration `M1E2SyncRetention`
(Merge-Regel aus `m01-mvp.md`); `ShoppingItems` mit `DeletedAt` älter 30 Tage hart löschen;
`HouseholdActivities` älter 90 Tage löschen; `ConsumptionEvents` bleiben (Statistik).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Sync/SyncChangesDto.cs`, `DeletedEntityDto.cs`, `SyncErrorCodes.cs` | neu | `SyncChangesDto(string NextToken, bool HasMore, bool IsFullSnapshot, IReadOnlyList<StorageLocationDto> StorageLocations, IReadOnlyList<ProductDto> Products, IReadOnlyList<BatchDto> Batches, IReadOnlyList<ShoppingListDto> ShoppingLists, IReadOnlyList<ShoppingItemDto> ShoppingItems, IReadOnlyList<StoreDto> Stores, IReadOnlyList<DeletedEntityDto> Deleted)`; `DeletedEntityDto(string EntityType, Guid Id)`; `sync.token_expired` (410) | – | 3×15 |
| `src/FoodAssistant.Application/Sync/ISyncFeedStore.cs` + `SyncFeedService.cs` | neu | Store: `GetChangesAsync(long after, int take)`, `GetMaxSequenceAsync`, `GetPrunedThroughAsync`, `LoadAsync(IReadOnlyCollection<(string Type, Guid Id)>)` bzw. `LoadAllAsync()` → DTO-Bündel; Service: Ablauf oben, `HouseholdToday` für Chargen-Status | `SyncFeedTests` (Server.Tests): Vollstand, Änderung danach, Löschung (Charge verbraucht, Eintrag soft-gelöscht, Produkt archiviert), mehrfach geänderte Entität nur einmal, `HasMore`, abgelaufenes Token 410, fremder Haushalt 404, Eingeschränkt erlaubt | 30+140 |
| `src/FoodAssistant.Infrastructure/Sync/SyncFeedStore.cs` | neu | EF; nutzt die vorhandenen Mapper (bei `internal` Mappern im selben Assembly direkt; Application-Mapper öffentlich) | – | 200 |
| `src/FoodAssistant.Infrastructure/Sync/SyncRetention.cs` + `…Configuration.cs` + Migration `M1E2SyncRetention` | neu | Tabelle `SyncRetention` | `MigrationTests` | 15+15 |
| `src/FoodAssistant.Application/Sync/RetentionRunner.cs` + `RetentionJob.cs` + `IRetentionStore.cs` | neu | Regeln oben; `ExecuteDeleteAsync`; Job täglich | `RetentionRunnerTests` (Infrastructure.Tests, feste Zeit) | 70+70+20 |
| `src/FoodAssistant.Infrastructure/Sync/RetentionStore.cs` | neu | EF, Löschungen im Haushaltsfilter | – | 80 |
| `src/FoodAssistant.Application/Sync/SyncLog.cs` | neu | `SyncTokenExpired` (3100, Information, –), `RetentionCompleted` (3101, Information, `HouseholdId`, `ChangeLogRows`, `ItemRows`, `ActivityRows`), `RetentionFailed` (3102, Warning, Exception) | – | 25 |
| Registrierungen (`HouseholdDataRegistration`/`HouseholdDataInfrastructureRegistration` – S0-Dateien, nach M1a frei) | ändern | Service, Store, Job | – | +5 |
| `src/FoodAssistant.Server/Sync/SyncEndpoints.cs` | ändern (Stub) | `GET /sync/changes?since&limit` (Any), Rate-Limit `api` | – | 40 |
| `src/FoodAssistant.Client/Sync/ISyncClient.cs` + `SyncClient.cs` | neu | `GetChangesAsync(Guid householdId, string? since, int? limit, …)` (für M2) | `SyncClientTests` | 15+40 |
| `docs/kb/datenschutz.md`, `docs/kb/telemetrie.md`, `docs/kb/datenmodell.md` | ändern | Aufbewahrung tatsächlich umgesetzt; Logs, Abläufe `foodassistant.sync.changes` (`foodassistant.changes`, `foodassistant.full`), `foodassistant.sync.retention` | – | – |

**Akzeptanz:** Tests grün; Migration neu erzeugt beim Merge; Snapshot aktualisiert.

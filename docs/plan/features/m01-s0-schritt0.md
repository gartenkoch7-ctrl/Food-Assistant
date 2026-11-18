# M1 S0 – Schritt 0: Datenmodell, Nahtstellen, Client- und UI-Basis

Gemeinsame Bausteine aller M1-Spuren. Umsetzung durch die steuernde Sitzung bzw. ihre
Subagenten direkt in `feat/m01-mvp`, Review-Schleife §9 über ganz Schritt 0, dann starten die Spuren.
Lehre aus M0: Querschnittsthemen (Fehlerabbildung, Versionen/ETags, Sync, Haushaltsumfang in
Hintergrunddiensten, Registrierungen) liegen **hier**, nicht in den Spuren.

## Aufgaben
- [x] S0.1 Datenmodell M1a, Domain-Ergänzungen, gemeinsame Contracts, Migration `M1Core`
- [x] S0.2 Nahtstellen: `OperationResult`, If-Match, Sync-Interceptor, SignalR-Hub, Kalender, Haushalts-Scopes, Verlauf, Bestands-Hook, Regel-Platzhalter
- [ ] S0.3 Einhängepunkte aller Spuren, Event-ID-Bereiche
- [x] S0.4 Client-Basis (HTTP, Antiforgery, traceparent, Idempotenz), `IAccountClient`, `ICatalogClient`, `ILiveUpdates`, Testprojekt
- [x] S0.5 UI-Basis: Anmeldung, Sitzung/Haushalt, gemeinsame Komponenten, Kategorien-Texte, „Mehr“-Links, Scan-Seite verschieben, Vorrendern aus

## Detailplan

Reihenfolge: **S0.1 → S0.2 → S0.3** (Backend-Kette) und **S0.4 → S0.5** (Frontend-Kette, Start
nach S0.1, parallel im Worktree). Danach Review der Gesamtänderung.

---

### Paket S0.1 – Datenmodell, Domain-Ergänzungen, Contracts, Migration

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** – · **Blockiert durch:** – (F1, F3 mit Annahme = Empfehlung; F14 Annahme a)
- **Muster:** Entitäten wie `Infrastructure/Households/Invitation.cs` + `InvitationConfiguration.cs`
  (Konfiguration `internal sealed class … : IEntityTypeConfiguration<T>`, Tabellenname Plural,
  Enums `HasConversion<string>().HasMaxLength(16)`, `Version` als `uint` + `IsRowVersion()` = `xmin`,
  `Id` mit `ValueGeneratedNever()`). Keine `DbSet`-Properties in `AppDbContext` (Stores nutzen
  `db.Set<T>()`); `ApplyConfigurationsFromAssembly` findet die Konfigurationen.

**Gemeinsame Spalten** (wenn nicht anders genannt): `Guid Id`, `Guid HouseholdId` (FK →
`Households`, `OnDelete Cascade`, implementiert `IHouseholdOwned`), `uint Version` (xmin),
`DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Inventory/StockBatchSnapshot.cs` | neu | `public sealed record StockBatchSnapshot(Guid Id, Quantity Quantity, StorageType StorageType, BatchState State, DateOnly? PackageExpiresOn, ExpiryKind PackageExpiryKind, DateOnly? ExpiresOn, ExpiryKind ExpiryKind, DateOnly? OpenedOn, DateOnly? FrozenOn, DateOnly? ThawedOn, DateOnly? PreparedOn)`; `public static StockBatchSnapshot From(StockBatch batch)` (nur öffentliche Getter); `public void Validate()` (`Id != Guid.Empty`, `Quantity.Unit != null`, Menge ≥ 0, `PackageExpiresOn.HasValue == (PackageExpiryKind != None)`, `ExpiresOn.HasValue == (ExpiryKind != None)`; sonst `ArgumentException`) | `StockBatchSnapshotTests`: Rundreise für jeden Zustand (Fabriken aus `tests/FoodAssistant.Domain.Tests/Inventory/TestBatches.cs`), ungültige Snapshots werfen | 50 |
| `src/FoodAssistant.Domain/Inventory/ShelfLifeDates.cs` | neu | **Aufteilung, weil `StockBatch.cs` schon 247 Zeilen hat:** reine Datumsberechnung aus `StockBatch` hierher (`static (DateOnly? Date, ExpiryKind Kind) FromRule(DateOnly from, int? days)`, `static (DateOnly? Date, ExpiryKind Kind) AfterOpening(DateOnly today, int? days, DateOnly? packageDate, ExpiryKind packageKind)` mit dem bestehenden Gleichstand-Kommentar) | bestehende `StockBatch*Tests` bleiben grün | 45 |
| `src/FoodAssistant.Domain/Inventory/StockBatch.cs` | ändern | `SetRuleDate`/Öffnungsdatum nutzen `ShelfLifeDates`; neu `public static StockBatch Restore(StockBatchSnapshot snapshot)` (`snapshot.Validate()`, dann Objektinitialisierer mit den privaten Settern, ~15 Zeilen). **Ziel: Datei ≤ 250 Zeilen nach S0.1** | (oben) | ±0 |
| `src/FoodAssistant.Domain/Inventory/BatchOrigin.cs` | neu | `enum BatchOrigin { Manual, Scanned, Purchase, Import, Leftover }` | – | 15 |
| `src/FoodAssistant.Domain/Catalog/ProductCategories.cs` | neu | `public static class ProductCategories`: Konstanten für die 24 Schlüssel (Tabelle unten), `IReadOnlyList<string> All`, `bool IsKnown(string? key)`, `StorageType DefaultStorageType(string key)` (Tabelle unten; unbekannt → `Ambient`) | `ProductCategoriesTests`: eindeutig, `^[a-z_]+$`, ≤ 32 Zeichen, jede Kategorie hat Lagertyp | 90 |
| `src/FoodAssistant.Domain/Shopping/StoreSections.cs` | neu | `public static class StoreSections`: 11 Schlüssel (Tabelle unten) in Standardreihenfolge, `All`, `IsKnown`, `string ForCategory(string categoryKey)` (unbekannt → `other`) | `StoreSectionsTests`: jede Kategorie bildet auf bekannten Bereich ab | 60 |
| `src/FoodAssistant.Domain/Shopping/ShoppingItemOrigin.cs` | neu | `enum ShoppingItemOrigin { Manual, MinimumStock, Recurring, MealPlan, Voice, Import }` | – | 15 |
| `src/FoodAssistant.Domain/Shopping/RecurrenceFrequency.cs` | neu | `enum RecurrenceFrequency { Daily, Weekly, Monthly }` | – | 10 |
| `src/FoodAssistant.Contracts/Common/QuantityDto.cs` | neu | `public sealed record QuantityDto(decimal Value, string Unit);` (ADR-0004: `{ "value": 1.5, "unit": "kg" }`) | – | 10 |
| `src/FoodAssistant.Contracts/Common/UnitCodes.cs` | neu | Konstanten `g, kg, ml, l, piece, tbsp, tsp, pinch, bunch, package, can` + `All` (gleiche Codes wie `Domain/Quantities/Unit.cs`) | `tests/FoodAssistant.Server.Tests/Api/UnitCodesTests.cs`: `UnitCodes.All` == `Unit.All.Select(u => u.Code)` | 30 |
| `src/FoodAssistant.Contracts/Inventory/{StorageTypeDto,BatchStateDto,ExpiryKindDto,ConsumptionKindDto,ConsumptionReasonDto,BatchOriginDto}.cs` | neu | Spiegel der Domain-Enums, je `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` wie `Contracts/Households/HouseholdRole.cs` | `ContractEnumTests` (Server.Tests/Api): Namen gleich den Domain-Enums | 6×12 |
| `src/FoodAssistant.Contracts/Shopping/ShoppingItemOriginDto.cs` | neu | Spiegel, String-Enum | (wie oben) | 12 |
| `src/FoodAssistant.Contracts/Catalog/ProductDto.cs` | neu | `public sealed record ProductDto(Guid Id, string Name, string? Brand, string CategoryKey, string DefaultUnit, Guid? DefaultStorageLocationId, string? SectionKey, QuantityDto? MinimumStock, IReadOnlyList<string> Barcodes, bool FromOpenFoodFacts, long Version);` | – | 20 |
| `src/FoodAssistant.Contracts/Catalog/ProductSummaryDto.cs` | neu | `(Guid Id, string Name, string? Brand, string CategoryKey, string DefaultUnit)` | – | 10 |
| `src/FoodAssistant.Contracts/Catalog/CreateProductRequest.cs` | neu | `(Guid? Id, string Name, string? Brand, string CategoryKey, string DefaultUnit, Guid? DefaultStorageLocationId, string? SectionKey, QuantityDto? MinimumStock, IReadOnlyList<string>? Barcodes, string? FoodFactsBarcode)` | – | 15 |
| `src/FoodAssistant.Contracts/Catalog/UpdateProductRequest.cs` | neu | wie Create ohne `Id`, `FoodFactsBarcode` | – | 15 |
| `src/FoodAssistant.Contracts/Catalog/StorageLocationDto.cs`, `StorageLocationRequest.cs`, `CategoryDto.cs` | neu | `StorageLocationDto(Guid Id, string Name, StorageTypeDto Type, int SortOrder, long Version)`; `StorageLocationRequest(Guid? Id, string Name, StorageTypeDto Type, int? SortOrder)`; `CategoryDto(string Key, string DefaultSectionKey, StorageTypeDto DefaultStorageType)` | – | 3×10 |
| `src/FoodAssistant.Contracts/Catalog/CatalogErrorCodes.cs` | neu | `catalog.product_not_found`, `catalog.location_not_found`, `catalog.validation_failed`, `catalog.barcode_in_use`, `catalog.product_exists`, `catalog.product_has_stock`, `catalog.location_in_use`, `catalog.location_type_locked` | – | 25 |
| `src/FoodAssistant.Contracts/Sync/HouseholdHubContract.cs` | neu | `public static class HouseholdHubContract { Path = "/hubs/household"; Join = "Join"; Leave = "Leave"; Changed = "Changed"; }` | – | 15 |
| `src/FoodAssistant.Contracts/Sync/HouseholdChangeDto.cs`, `HouseholdChangesMessage.cs`, `SyncEntityTypes.cs` | neu | `HouseholdChangeDto(string EntityType, Guid Id, long Sequence, bool Deleted)`; `HouseholdChangesMessage(Guid HouseholdId, IReadOnlyList<HouseholdChangeDto> Changes)`; Konstanten `storage_location, product, batch, shopping_list, shopping_item, store` | – | 3×12 |
| `src/FoodAssistant.Infrastructure/Persistence/ISyncTracked.cs` | neu | `public interface ISyncTracked : IHouseholdOwned { Guid Id { get; } }` – Doku: Interceptor (S0.2) schreibt Änderungsprotokoll | – | 15 |
| `src/FoodAssistant.Infrastructure/Persistence/ISoftDeletable.cs` | neu | `public interface ISoftDeletable { DateTimeOffset? DeletedAt { get; } }` | – | 10 |
| `src/FoodAssistant.Infrastructure/Catalog/StorageLocation.cs` + `StorageLocationConfiguration.cs` | neu | Tabelle `StorageLocations`: `Name` (80, Pflicht), `Type` (`StorageType`, string 16), `SortOrder int`; Index `(HouseholdId, SortOrder)`; `ISyncTracked` | – | 30+25 |
| `src/FoodAssistant.Infrastructure/Catalog/Product.cs` + `ProductConfiguration.cs` | neu | Tabelle `Products`: `Name` (120, Pflicht), `Brand` (120)?, `CategoryKey` (32, Pflicht), `DefaultUnitCode` (16, Pflicht), `DefaultStorageLocationId Guid?` (FK SetNull), `SectionKey` (32)?, `MinimumStockValue decimal(12,3)?` + `MinimumStockUnitCode` (16)? (Check-Constraint: beide oder keins), `MinimumStockTriggered bool`, `LastShelfLifeDays int?`, `LastExpiryKind ExpiryKind?` (string 16), `FoodFactsBarcode` (14)?, `ArchivedAt DateTimeOffset?`, Navigation `ICollection<ProductBarcode> Barcodes`; Index `(HouseholdId, Name)`; `ISyncTracked` | – | 45+45 |
| `src/FoodAssistant.Infrastructure/Catalog/ProductBarcode.cs` + `ProductBarcodeConfiguration.cs` | neu | Tabelle `ProductBarcodes`: `Id`, `HouseholdId`, `ProductId` (FK Cascade), `Code` (14, Pflicht); Unique `(HouseholdId, Code)`; nur `IHouseholdOwned` (Änderungen laufen über das Produkt, Regel für A: Produkt-`UpdatedAt` immer mit setzen) | – | 20+20 |
| `src/FoodAssistant.Infrastructure/Inventory/StockBatchRecord.cs` + `StockBatchRecordConfiguration.cs` | neu | Tabelle `StockBatches`: `ProductId` (FK Restrict), `StorageLocationId` (FK Restrict), `QuantityValue decimal(12,3)`, `QuantityUnitCode` (16), `StorageType`, `State`, `PackageExpiresOn DateOnly?`, `PackageExpiryKind`, `ExpiresOn DateOnly?`, `ExpiryKind`, `OpenedOn/FrozenOn/ThawedOn/PreparedOn DateOnly?`, `Origin` (`BatchOrigin`), `AddedOn DateOnly`; Indizes `(HouseholdId, ProductId)`, `(HouseholdId, ExpiresOn)`; `ISyncTracked` | – | 50+45 |
| `src/FoodAssistant.Infrastructure/Inventory/StockBatchMapping.cs` | neu | `internal static class StockBatchMapping`: `StockBatch ToDomain(this StockBatchRecord r)` (über `StockBatch.Restore`, Einheit über `Unit.FromCode`), `void Apply(this StockBatchRecord r, StockBatch batch, DateTimeOffset now)` (alle Snapshot-Felder + `UpdatedAt`), `StockBatchRecord NewRecord(StockBatch batch, Guid householdId, Guid productId, Guid storageLocationId, BatchOrigin origin, DateOnly addedOn, DateTimeOffset now)` | `StockBatchMappingTests` (Infrastructure.Tests, ohne DB): Rundreise | 70 |
| `src/FoodAssistant.Infrastructure/Inventory/ConsumptionEvent.cs` + `ConsumptionEventConfiguration.cs` | neu | Tabelle `ConsumptionEvents`: `ProductId` (FK Restrict), `BatchId Guid?` (kein FK), `Kind`, `Reason?`, `QuantityValue`, `QuantityUnitCode`, `ExpiryStatusAtEvent` (`ExpiryStatus`, string 16), `UserId Guid?` (kein FK), `OccurredAt`, `OccurredOn DateOnly` (Haushaltsdatum), `UndoneAt?`, `Version` (xmin); kein `UpdatedAt`; Indizes `(HouseholdId, OccurredOn)`, `(HouseholdId, ProductId, OccurredAt)` | – | 35+35 |
| `src/FoodAssistant.Infrastructure/ShelfLife/ShelfLifeOverride.cs` + `ShelfLifeOverrideConfiguration.cs` | neu | Tabelle `ShelfLifeOverrides`: `CategoryKey` (32)?, `ProductId Guid?` (FK Cascade), `StorageType`, `DaysAfterOpening/DaysFrozen/DaysAfterThawing/DaysAsLeftover int?`; Check `("CategoryKey" IS NULL) <> ("ProductId" IS NULL)`; Unique gefiltert `(HouseholdId, CategoryKey, StorageType)` bzw. `(HouseholdId, ProductId, StorageType)` | – | 30+35 |
| `src/FoodAssistant.Infrastructure/Shopping/ShoppingList.cs` + `ShoppingListConfiguration.cs` | neu | Tabelle `ShoppingLists`: `Name` (80, Pflicht, leer erlaubt = automatische Standardliste), `StoreId Guid?` (FK SetNull), `IsDefault bool`, `SortOrder int`; Unique gefiltert `(HouseholdId) WHERE "IsDefault"`; `ISyncTracked` | – | 30+30 |
| `src/FoodAssistant.Infrastructure/Shopping/ShoppingItem.cs` + `ShoppingItemConfiguration.cs` | neu | Tabelle `ShoppingItems`: `ListId` (FK Cascade), `ProductId Guid?` (FK SetNull), `Text` (200, Pflicht), `QuantityValue decimal(12,3)?`, `QuantityUnitCode` (16)?, `Note` (500)?, `SectionKey` (32, Pflicht), `Origin` (`ShoppingItemOrigin`), `Checked bool`, `CheckedAt?`, `CheckedByUserId Guid?`, `CreatedByUserId Guid?`, **`long Version` mit `IsConcurrencyToken()`** (kein xmin; Konfliktregeln ADR-0005), `UpdatedAt`, `DeletedAt?`; benannter Filter `AppDbContext.NotDeletedFilter` (`DeletedAt == null`); Indizes `(HouseholdId, ListId)`, `(HouseholdId, ProductId) WHERE "DeletedAt" IS NULL AND NOT "Checked"`; `ISyncTracked`, `ISoftDeletable` | – | 45+45 |
| `src/FoodAssistant.Infrastructure/Shopping/RecurringShoppingRule.cs` + `…Configuration.cs` | neu | Tabelle `RecurringShoppingRules`: `ListId` (FK Cascade), `ProductId?` (FK SetNull), `Text` (200), `QuantityValue?`, `QuantityUnitCode?`, `Frequency` (`RecurrenceFrequency`), `DayOfWeek int?`, `DayOfMonth int?`, `NextDueOn DateOnly`, `LastCreatedItemId Guid?` | – | 30+30 |
| `src/FoodAssistant.Infrastructure/Stores/Store.cs` + `StoreConfiguration.cs` | neu | Tabelle `Stores`: `Name` (80), `LastCheckedSectionKey` (32)?, `LastCheckedAt?`; `ISyncTracked` | – | 25+20 |
| `src/FoodAssistant.Infrastructure/Stores/StoreSectionOrder.cs` + `…Configuration.cs` | neu | Tabelle `StoreSectionOrders`: `HouseholdId`, `StoreId` (FK Cascade), `SectionKey` (32), `Position int`; PK `(StoreId, SectionKey)`; kein Id/Version | – | 15+20 |
| `src/FoodAssistant.Infrastructure/Stores/StoreSectionTransition.cs` + `…Configuration.cs` | neu | Tabelle `StoreSectionTransitions`: `HouseholdId`, `StoreId` (FK Cascade), `FromSectionKey`, `ToSectionKey`, `Count int`; PK `(StoreId, FromSectionKey, ToSectionKey)` | – | 15+20 |
| `src/FoodAssistant.Infrastructure/Sync/ChangeLogEntry.cs` + `ChangeLogEntryConfiguration.cs` | neu | Tabelle `ChangeLog`: `long Sequence` (Identity, PK), `HouseholdId` (FK Cascade), `EntityType` (32), `EntityId`, `Deleted bool`, `ChangedAt`; Index `(HouseholdId, Sequence)`; `IHouseholdOwned` | – | 20+20 |
| `src/FoodAssistant.Infrastructure/Activity/HouseholdActivity.cs` + `…Configuration.cs` | neu | Tabelle `HouseholdActivities`: `Area` (`ActivityArea` aus `Application/Activity/ActivityArea.cs`, string 16), `Kind` (48), `SubjectId`, `ProductId?`, `UserId?`, `OccurredAt`, `PayloadJson` (`jsonb`)?, `SubjectVersionAfter long?`, `UndoneAt?`, `Version` (xmin); Index `(HouseholdId, OccurredAt)` | – | 30+30 |
| `src/FoodAssistant.Application/Activity/ActivityArea.cs` | neu | `enum ActivityArea { Inventory, Shopping, Catalog }` | – | 10 |
| `src/FoodAssistant.Infrastructure/FoodFacts/FoodFactsCacheEntry.cs` + `…Configuration.cs` | neu | Tabelle `FoodFactsCache` (**serverweit**, kein `IHouseholdOwned` – Kommentar mit Verweis auf KB externe-daten): `Barcode` (14, PK), `Found bool`, `PayloadJson` (`jsonb`)?, `FetchedAt`, `ExpiresAt`, `LastRequestedAt` | – | 20+20 |
| `src/FoodAssistant.Infrastructure/Households/Household.cs` + `HouseholdConfiguration.cs` | ändern | `public int ExpiryLeadDays { get; set; } = 2;` + `HasDefaultValue(2)` | – | +5 |
| `src/FoodAssistant.Infrastructure/Persistence/Migrations/<ts>_M1Core*.cs` | neu (generiert) | `dotnet ef migrations add M1Core …` | `MigrationTests` (bestehend) grün | gen. |
| `tests/FoodAssistant.Infrastructure.Tests/M1SchemaTests.cs` | neu | Haushaltsfilter für jede neue `IHouseholdOwned`-Entität (zwei Haushalte, je ein Datensatz, nur eigener sichtbar – Muster `HouseholdQueryFilterTests.cs`), Unique-Index Standardliste, Check-Constraint Override, `NotDeletedFilter` auf `ShoppingItems` | ja | 200 |
| `tests/FoodAssistant.Infrastructure.Tests/M1Seed.cs` | neu | `internal static class M1Seed`: `StorageLocationAsync`, `ProductAsync`, `BatchAsync`, `ShoppingListAsync`, `ShoppingItemAsync`, `StoreAsync` (Muster `Seed.cs`) | – | 120 |
| `tests/FoodAssistant.Server.Tests/Support/HouseholdDataSeed.cs` | neu | Seeding für API-Tests direkt über `AppDbContext` aus `ServerTestApp.Services` mit gesetztem `HttpHouseholdContext.SetMembership(new(householdId, Guid.Empty, HouseholdRole.Owner))`; gleiche Methoden wie `M1Seed` | – | 130 |

**Kategorien (F1, Annahme a)** – Schlüssel · DE · EN · Standard-Lagertyp · Standardbereich:
`dairy` Milchprodukte/Dairy · Chilled · `dairy_chilled`; `cheese` Käse/Cheese · Chilled · `dairy_chilled`;
`eggs` Eier/Eggs · Chilled · `dairy_chilled`; `meat` Fleisch/Meat · Chilled · `meat_fish`;
`poultry` Geflügel/Poultry · Chilled · `meat_fish`; `fish` Fisch & Meeresfrüchte/Fish & seafood · Chilled · `meat_fish`;
`sausage` Wurst & Aufschnitt/Sausage & cold cuts · Chilled · `meat_fish`; `fruit` Obst/Fruit · Ambient · `produce`;
`vegetables` Gemüse/Vegetables · Chilled · `produce`; `herbs` Kräuter/Herbs · Chilled · `produce`;
`bread` Brot & Backwaren/Bread & bakery · Ambient · `bakery`; `grains` Nudeln, Reis & Getreide/Pasta, rice & grains · Ambient · `pantry`;
`legumes` Hülsenfrüchte/Legumes · Ambient · `pantry`; `canned` Konserven/Canned food · Ambient · `pantry`;
`frozen` Tiefkühlkost/Frozen food · Frozen · `frozen`; `ready_meals` Fertiggerichte (gekühlt)/Ready meals (chilled) · Chilled · `dairy_chilled`;
`sauces` Saucen & Würzmittel/Sauces & condiments · Ambient · `pantry`; `spices` Gewürze/Spices · Ambient · `spices_baking`;
`oils` Öle & Fette/Oils & fats · Ambient · `pantry`; `baking` Backzutaten/Baking · Ambient · `spices_baking`;
`sweets` Süßes & Snacks/Sweets & snacks · Ambient · `sweets_snacks`; `beverages` Getränke/Beverages · Ambient · `beverages`;
`plant_based` Pflanzliche Alternativen/Plant-based alternatives · Chilled · `dairy_chilled`; `other` Sonstiges/Other · Ambient · `other`.

**Bereiche (F3, Annahme a)** in Standardreihenfolge: `produce` Obst & Gemüse, `bakery` Brot & Backwaren,
`dairy_chilled` Kühlregal, `meat_fish` Fleisch, Wurst & Fisch, `frozen` Tiefkühl, `pantry`
Grundnahrungsmittel & Konserven, `spices_baking` Gewürze & Backen, `sweets_snacks` Süßes & Snacks,
`beverages` Getränke, `household` Drogerie & Haushalt, `other` Sonstiges.

**Akzeptanz:** Build ohne Warnungen; `MigrationTests`, `M1SchemaTests`, Domain-Tests grün;
`dotnet ef migrations list` zeigt `M1Core`.
**Nicht Teil:** Stores/Services/Endpunkte der Features (gehören den Spuren).

---

### Paket S0.2 – Nahtstellen

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** S0.1 · **Blockiert durch:** –
- **Muster:** Dienste wie `Application/Households/MemberService.cs`; Logs wie
  `Infrastructure/Persistence/PersistenceLog.cs`; Fehler über `Server/Api/Errors/ApiProblems.cs`;
  ETags `Server/Api/Concurrency/ETags.cs`; Registrierung wie `Application/Composition/AccountsRegistration.cs`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/Common/OperationStatus.cs` | neu | `enum OperationStatus { Ok, Created, Deleted, NotFound, Invalid, Conflict, RuleViolated, VersionMismatch, Forbidden }` | – | 15 |
| `src/FoodAssistant.Application/Common/OperationResult.cs` | neu | `public sealed record OperationResult<T>` mit `Status`, `T? Value`, `string? ErrorCode`, `IReadOnlyDictionary<string,string[]>? Errors`, `long? CurrentVersion`; Fabriken `Ok(T)`, `Created(T)`, `Deleted()`, `NotFound(string code)`, `Invalid(IReadOnlyDictionary<string,string[]> errors, string code)`, `Conflict(string code)`, `RuleViolated(string code)`, `VersionMismatch(long currentVersion, T? current)`, `Forbidden(string code)` | `OperationResultTests` (Application.Tests) | 70 |
| `src/FoodAssistant.Server/Api/Results/OperationResults.cs` | neu | `public static IResult ToHttpResult<T>(this OperationResult<T> result, HttpContext http, Func<T, long>? versionOf = null, Func<T, string>? locationOf = null)`: Ok→200 (+ETag über `ETags.SetETag`), Created→201 (+ETag, Location), Deleted→204, NotFound→`ApiProblems.NotFound(code)`, Invalid→`ApiProblems.ValidationFailed(errors, errorCode: code)`, Conflict→409, RuleViolated→422, VersionMismatch→`ETags.PreconditionFailed(http, current, value)`, Forbidden→403 mit `errorCode` | `OperationResultsTests` (Server.Tests/Api, über `TestEndpoints.cs`-Muster) | 80 |
| `src/FoodAssistant.Server/Api/Concurrency/IfMatch.cs` | neu | `public static bool TryRead(HttpContext http, bool required, out long? expectedVersion, out IResult? problem)`: fehlt → 428 (`required`) bzw. `null`; `*` → `null`; genau ein starkes/schwaches ETag mit Zahl → Wert; sonst 400 (Wortlaut wie `ETags.CheckIfMatch`) | `IfMatchTests` | 60 |
| `src/FoodAssistant.Application/Sync/HouseholdChange.cs` | neu | `public sealed record HouseholdChange(string EntityType, Guid EntityId, long Sequence, bool Deleted);` | – | 10 |
| `src/FoodAssistant.Application/Sync/IHouseholdChangeNotifier.cs` + `NullHouseholdChangeNotifier.cs` | neu | `Task PublishAsync(Guid householdId, IReadOnlyList<HouseholdChange> changes, CancellationToken ct)`; Null-Implementierung tut nichts | – | 15+15 |
| `src/FoodAssistant.Infrastructure/Sync/SyncEntityTypeMap.cs` | neu | `internal static class`: `string KeyOf(Type entityType)` für `StorageLocation`→`storage_location`, `Product`→`product`, `StockBatchRecord`→`batch`, `ShoppingList`, `ShoppingItem`, `Store` (Konstanten aus `Contracts/Sync/SyncEntityTypes`); unbekannt → `InvalidOperationException` | `SyncEntityTypeMapTests`: jede `ISyncTracked`-Klasse im Modell hat einen Schlüssel | 40 |
| `src/FoodAssistant.Infrastructure/Sync/PendingChangeNotifications.cs` | neu | scoped; sammelt `ChangeLogEntry` pro Save/Transaktion: `Add`, `TakeAll()`, `Clear()` | – | 35 |
| `src/FoodAssistant.Infrastructure/Sync/SyncChangeInterceptor.cs` | neu | `internal sealed class SyncChangeInterceptor(PendingChangeNotifications pending, IHouseholdChangeNotifier notifier, TimeProvider time, ILogger<SyncChangeInterceptor> logger) : SaveChangesInterceptor`. `SavingChangesAsync`: Einträge `ChangeTracker.Entries<ISyncTracked>()` mit Zustand Added/Modified/Deleted **erst in Liste kopieren**, dann je ein `ChangeLogEntry` (`Deleted` = Zustand Deleted oder `ISoftDeletable.DeletedAt != null`) per `context.Add` anhängen und in `pending` merken. `SavedChangesAsync`: ohne laufende Transaktion (`context.Database.CurrentTransaction is null`) → veröffentlichen. `SaveChangesFailedAsync` → `pending.Clear()`. Veröffentlichen: nach `HouseholdId` gruppieren, `notifier.PublishAsync`; Ausnahmen fangen und `SyncLog.ChangeNotificationFailed` (nie die Speicherung scheitern lassen) | `SyncChangeInterceptorTests` (Testcontainers): Added/Modified/Deleted/Soft-Delete → Zeilen; Notifier erhält `Sequence > 0`; in Transaktion erst nach Commit, nach Rollback nie (Fake-Notifier) | 120 |
| `src/FoodAssistant.Infrastructure/Sync/SyncTransactionInterceptor.cs` | neu | `DbTransactionInterceptor`: `TransactionCommittedAsync` → veröffentlichen (gleiche Methode, in `SyncChangePublisher` ausgelagert), `TransactionRolledBackAsync`/`TransactionFailedAsync` → verwerfen | (oben) | 50 |
| `src/FoodAssistant.Infrastructure/Sync/SyncChangePublisher.cs` | neu | gemeinsame Veröffentlichung für beide Interceptoren | – | 45 |
| `src/FoodAssistant.Infrastructure/Sync/SyncLog.cs` | neu | `ChangeNotificationFailed` (2200, Warning, `HouseholdId`, Exception) | – | 20 |
| `src/FoodAssistant.Infrastructure/Composition/PersistenceRegistration.cs` | ändern | im `AddDbContext`-Lambda `options.AddInterceptors(services.GetRequiredService<SyncChangeInterceptor>(), services.GetRequiredService<SyncTransactionInterceptor>())` | – | +3 |
| `src/FoodAssistant.Application/Households/HouseholdToday.cs` + `IHouseholdCalendar.cs` | neu | `public sealed record HouseholdToday(DateOnly Today, int ExpiryLeadDays, string TimeZoneId);` · `Task<HouseholdToday> GetTodayAsync(CancellationToken ct)` (aktiver Haushalt; ohne → `InvalidOperationException`) | – | 10+15 |
| `src/FoodAssistant.Infrastructure/Households/HouseholdCalendar.cs` | neu | liest `Households` (Id = aktiver Haushalt), rechnet `TimeZoneInfo.ConvertTime(time.GetUtcNow(), tz)` → `DateOnly`; Ergebnis pro Scope zwischengespeichert | `HouseholdCalendarTests`: Zeitzonenwechsel um Mitternacht (Europe/Berlin vs. UTC) mit `MutableTimeProvider`-Muster | 50 |
| `src/FoodAssistant.Application/Households/IHouseholdScopeFactory.cs` | neu | `AsyncServiceScope CreateScope(Guid householdId)`; Doku: für Hintergrunddienste; Person = `HouseholdScopes.SystemUserId` | – | 15 |
| `src/FoodAssistant.Application/Households/HouseholdScopes.cs` | neu | `public static class HouseholdScopes { public static readonly Guid SystemUserId = Guid.Empty; }` | – | 10 |
| `src/FoodAssistant.Server/Households/HouseholdScopeFactory.cs` | neu | erzeugt Scope, `GetRequiredService<HttpHouseholdContext>().SetMembership(new(householdId, HouseholdScopes.SystemUserId, HouseholdRole.Owner))` | `HouseholdScopeFactoryTests` (Server.Tests): Abfrage im Scope sieht nur diesen Haushalt | 30 |
| `src/FoodAssistant.Application/Households/IHouseholdStore.cs` + `Infrastructure/Households/HouseholdStore.cs` | ändern | `Task<IReadOnlyList<Guid>> ListActiveHouseholdIdsAsync(CancellationToken ct)` (nicht gelöschte) | Test in `M1SchemaTests` | +15 |
| `src/FoodAssistant.Application/Activity/ActivityEntry.cs` + `IActivityLog.cs` | neu | `ActivityEntry(ActivityArea Area, string Kind, Guid SubjectId, Guid? ProductId, Guid? UserId, string? PayloadJson, long? SubjectVersionAfter)` · `Guid Add(ActivityEntry entry)` – „wird mit dem nächsten `SaveChanges` desselben Requests gespeichert“, liefert die neue Id (für Rückgängig) | – | 15+15 |
| `src/FoodAssistant.Infrastructure/Activity/ActivityLog.cs` | neu | `db.Add(new HouseholdActivity{…})` ohne Speichern; `HouseholdId` aus `IHouseholdContext`, `OccurredAt` aus `TimeProvider`; `UserId == SystemUserId` → `null` | `ActivityLogTests` | 40 |
| `src/FoodAssistant.Application/Inventory/IStockChangedHandler.cs` | neu | `Task OnStockChangedAsync(Guid householdId, IReadOnlyCollection<Guid> productIds, CancellationToken ct)`; Doku: Aufrufer (B2, F, A bei Mindestbestand, L) rufen **alle** registrierten Handler **nach** erfolgreichem Speichern; Fehler eines Handlers werden geloggt, nicht weitergereicht | – | 20 |
| `src/FoodAssistant.Application/ShelfLife/IShelfLifeRules.cs` + `NoShelfLifeRules.cs` | neu | `Task<ShelfLifeRule> ResolveAsync(Guid productId, StorageType storageType, CancellationToken ct)`; `Task<ShelfLifeRule> ResolveForCategoryAsync(string categoryKey, StorageType storageType, CancellationToken ct)`; Platzhalter liefert `ShelfLifeRule.Empty` (= ohne Datum, sicher) | – | 20+15 |
| `src/FoodAssistant.Application/Composition/HouseholdDataRegistration.cs` | neu | `AddHouseholdData(this IServiceCollection)`: `TryAddSingleton<IHouseholdChangeNotifier, NullHouseholdChangeNotifier>`, `TryAddScoped<IShelfLifeRules, NoShelfLifeRules>` (B1 registriert später mit `AddScoped` und gewinnt) | – | 25 |
| `src/FoodAssistant.Application/Composition/ApplicationRegistration.cs` | ändern | `services.AddHouseholdData();` vor allen Feature-Aufrufen | – | +1 |
| `src/FoodAssistant.Infrastructure/Composition/HouseholdDataInfrastructureRegistration.cs` | neu | `AddHouseholdDataPersistence(this IHostApplicationBuilder)`: scoped `PendingChangeNotifications`, `SyncChangeInterceptor`, `SyncTransactionInterceptor`, `IHouseholdCalendar`, `IActivityLog` | – | 25 |
| `src/FoodAssistant.Infrastructure/Composition/InfrastructureRegistration.cs` | ändern | Aufruf `AddHouseholdDataPersistence()` | – | +1 |
| `src/FoodAssistant.Server/Sync/HouseholdHub.cs` | neu | `[Authorize] internal sealed class HouseholdHub(IHouseholdStore households, ILogger<HouseholdHub> logger) : Hub`; `public async Task Join(Guid householdId)`: Person per `Context.User!.GetRequiredUserId()`, `FindMembershipAsync`; keine Mitgliedschaft → `SyncLog.HubJoinDenied`, `throw new HubException(HouseholdErrorCodes.NotFound)`; sonst `Groups.AddToGroupAsync(ConnectionId, GroupName(id))`; `public Task Leave(Guid householdId)`; `internal static string GroupName(Guid id) => $"household:{id:N}"` | `HouseholdHubTests`: Mitglied tritt bei und erhält `Changed` nach Produktänderung (Seed + Update über `AppDbContext`); Nichtmitglied → `HubException`; ohne Anmeldung → Verbindung abgelehnt. Transport LongPolling über `factory.Server.CreateHandler()`, Anmeldung per Cookie-Header des `TestClient` oder Access-Token aus `Auth/OidcFlow.cs` | 60 |
| `src/FoodAssistant.Server/Sync/SignalRHouseholdChangeNotifier.cs` | neu | `IHubContext<HouseholdHub>`; sendet `HouseholdHubContract.Changed` mit `HouseholdChangesMessage` an die Gruppe | (oben) | 35 |
| `src/FoodAssistant.Server/Sync/SyncLog.cs` | neu | `HubJoinDenied` (1300, Information, –), `HubJoined` (1301, Debug, `HouseholdId`) | – | 20 |
| `src/FoodAssistant.Server/Composition/LiveRegistration.cs` | neu | `AddLive(this WebApplicationBuilder)`: `AddSignalR()`, `AddSingleton<IHouseholdChangeNotifier, SignalRHouseholdChangeNotifier>()`, `AddScoped<IHouseholdScopeFactory, HouseholdScopeFactory>()`; `MapLive(this WebApplication)`: `MapHub<HouseholdHub>(HouseholdHubContract.Path).RequireRateLimiting(RateLimitPolicies.Api)` | – | 40 |
| `src/FoodAssistant.Server/Program.cs` | ändern | `builder.AddLive();` nach `AddAuth()`; `app.MapLive();` nach `MapApiV1()` | – | +2 |
| `Directory.Packages.props`, `tests/FoodAssistant.Server.Tests/*.csproj` | ändern | `Microsoft.AspNetCore.SignalR.Client` 10.0.14 (MIT; Hub-Tests, später Web-Client E1) | – | +2 |
| `docs/kb/telemetrie.md` | ändern | neue Logs eintragen | – | +4 |

**Muster für Stores (in `docs/kb/datenmodell.md` neu anlegen, hier verbindlich):**
1. Laden mit Haushaltsfilter; nicht gefunden → `NotFound`.
2. `expectedVersion` (aus `IfMatch.TryRead`) ≠ `entity.Version` → `VersionMismatch(entity.Version, dto)`.
3. Vor dem Speichern `db.Entry(entity).Property(e => e.Version).OriginalValue = (uint)expected`, damit
   ein Wettlauf zwischen Laden und Speichern als `DbUpdateConcurrencyException` auffällt → neu laden,
   `VersionMismatch`.
4. Nach dem Speichern `IStockChangedHandler` bzw. andere Hooks aufrufen.

**Akzeptanz:** alle neuen Tests grün; zwei Browser-unabhängige Hub-Tests; `docs/kb/datenmodell.md`
angelegt (Entitäten-Übersicht aus S0.1 + Store-Muster) und in `docs/kb/README.md` verlinkt.

---

### Paket S0.3 – Einhängepunkte, Event-ID-Bereiche

- **Stufe / Modell / Effort:** 1 / Haiku / low
- **Wartet auf:** S0.2 · **Muster:** `Application/Composition/AccountsRegistration.cs`,
  `Server/Households/MemberEndpoints.cs` (Signatur), `Infrastructure/Composition/AccountsInfrastructureRegistration.cs`

Jede Stub-Methode prüft nur `ArgumentNullException.ThrowIfNull(...)` und gibt das Argument zurück.
Doc-Kommentar: „Einhängepunkt für Spur X (docs/plan/m01-mvp.md)“.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/Composition/{Catalog,ShelfLife,Inventory,FoodFacts,Shopping,Checkout,Restock,Notifications,Statistics,Labels,Import,Backup,Export,Activity}Registration.cs` | neu | je `public static IServiceCollection Add<Feature>(this IServiceCollection services)` | – | 14×15 |
| `src/FoodAssistant.Application/Composition/ApplicationRegistration.cs` | ändern | Aufrufe in dieser Reihenfolge nach `AddHouseholdData()` | – | +14 |
| `src/FoodAssistant.Infrastructure/Composition/{Feature}InfrastructureRegistration.cs` (gleiche 14 Features) | neu | je `public static IHostApplicationBuilder Add<Feature>Persistence(this IHostApplicationBuilder builder)` | – | 14×15 |
| `src/FoodAssistant.Infrastructure/Composition/InfrastructureRegistration.cs` | ändern | Aufrufe | – | +14 |
| `src/FoodAssistant.Server/{Catalog,ShelfLife,Inventory,FoodFacts,Shopping,Stores,Checkout,Restock,Notifications,Statistics,Labels,Import,Export,Activity,Sync}/{Feature}Endpoints.cs` | neu | je `internal static class <Feature>Endpoints { public static RouteGroupBuilder Map<Feature>Endpoints(this RouteGroupBuilder household) => household; }` (Sync: `MapSyncEndpoints`) | – | 15×12 |
| `src/FoodAssistant.Server/Households/HouseholdSettingsEndpoints.cs` | neu | `MapHouseholdSettingsEndpoints(this RouteGroupBuilder household)` (Spur K) | – | 12 |
| `src/FoodAssistant.Server/Households/HouseholdImportEndpoints.cs` | neu | `MapHouseholdImportEndpoints(this RouteGroupBuilder households)` (Spur N, unter `/households`) | – | 12 |
| `src/FoodAssistant.Server/Notifications/NotificationApiEndpoints.cs`, `Server/Backup/BackupEndpoints.cs` | neu | `MapNotificationApiEndpoints(this RouteGroupBuilder api)`, `MapBackupEndpoints(this RouteGroupBuilder api)` (unter `/api/v1`) | – | 2×12 |
| `src/FoodAssistant.Server/Households/HouseholdEndpoints.cs` | ändern | nach `household.MapMemberEndpoints();` alle `Map…Endpoints()` der Haushaltsgruppe; `households.MapHouseholdImportEndpoints();` | – | +18 |
| `src/FoodAssistant.Server/Api/ApiV1Routes.cs` | ändern | `group.MapNotificationApiEndpoints(); group.MapBackupEndpoints();` | – | +2 |
| `tests/FoodAssistant.Architecture.Tests/TelemetryConventionTests.cs` | ändern | `s_ranges` um alle Zeilen der Tabelle „Event-ID-Bereiche“ in `docs/plan/m01-mvp.md` ergänzen (Präfixe `FoodAssistant.Server.Sync` … `FoodAssistant.Mobile`, UI **und** Client je Feature mit gleichem Bereich) | bestehende Tests grün (keine Überlappung) | +45 |
| `docs/kb/telemetrie.md` | ändern | Tabelle „EventId-Bereiche“ um dieselben Zeilen | – | +30 |

**Akzeptanz:** Build, `TelemetryConventionTests`, OpenAPI-Snapshot unverändert (Stubs mappen nichts).

---

### Paket S0.4 – Client-Basis

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** S0.1 (Contracts) · **Muster:** Problem-Lesen wie
  `tests/FoodAssistant.Server.Tests/Auth/TestClient.cs`; Antiforgery-Ablauf wie `TestClient.RefreshAntiforgeryAsync`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Http/ApiError.cs` | neu | `public sealed record ApiError(int Status, string ErrorCode, string? Detail, IReadOnlyDictionary<string, string[]> FieldErrors, string? CurrentETag)`; `bool IsNetworkError => Status == 0`; Konstanten `ClientErrorCodes.Network = "client.network"`, `Offline = "client.offline"` (in eigener Datei `ClientErrorCodes.cs`) | – | 25+15 |
| `src/FoodAssistant.Client/Http/ApiResult.cs` | neu | `public sealed record ApiResult<T>(T? Value, string? ETag, ApiError? Error) { bool IsSuccess; static Success(T, string?); static Failure(ApiError) }` und `ApiResult` (ohne Wert) | – | 40 |
| `src/FoodAssistant.Client/Http/ApiHttpClient.cs` | neu | `public sealed class ApiHttpClient(HttpClient http)`: `Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct)`; `Task<ApiResult<TResponse>> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest? body, string? ifMatch, Guid? idempotencyKey, CancellationToken ct)`; `Task<ApiResult> SendAsync<TRequest>(…)` ohne Antwortkörper; `Task<ApiResult<Stream>> GetStreamAsync(string path, CancellationToken ct)`. Schreibende Methoden setzen immer `Idempotency-Key` (`idempotencyKey ?? Guid.CreateVersion7()`); `If-Match` wenn angegeben; liest `ETag`. Fehler: `application/problem+json` → `errorCode`, `errors`, `currentETag`, `detail`; sonst `ErrorCode = "common.request_failed"`; `HttpRequestException` → `ApiError(0, ClientErrorCodes.Network, …)`. JSON: `JsonSerializerDefaults.Web` | `ApiHttpClientTests` mit Fake-`HttpMessageHandler`: Erfolg+ETag, Problem mit Feldfehlern, 412 mit `currentETag`, Netzwerkfehler, Idempotency-Key gesetzt | 160 |
| `src/FoodAssistant.Client/Http/AntiforgeryHandler.cs` | neu | `DelegatingHandler`: für POST/PUT/PATCH/DELETE Token holen (`GET /api/v1/auth/antiforgery` → `AntiforgeryTokenResponse`), Header setzen; bei 400 mit `accounts.antiforgery_invalid` einmal neu holen und wiederholen (Inhalt vorher puffern, Anfrage klonen) | `AntiforgeryHandlerTests` | 90 |
| `src/FoodAssistant.Client/Http/TraceContextHandler.cs` | neu | setzt `traceparent`, falls nicht vorhanden: aus `Activity.Current` (W3C), sonst neu `00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01` (AGENTS.md 2.5) | `TraceContextHandlerTests` | 40 |
| `src/FoodAssistant.Client/Http/ApiPaths.cs` | neu | `public static string Household(Guid householdId, string relative)` → `api/v1/households/{id}/{relative}`; `Escape(string)` für Pfadsegmente | – | 25 |
| `src/FoodAssistant.Client/Accounts/IAccountClient.cs` + `AccountClient.cs` | neu | `GetMeAsync`, `LoginAsync(LoginRequest)`, `LogoutAsync`, `GetSetupStatusAsync` (Spur K ergänzt Einrichtung/Einladung) | `AccountClientTests` | 20+60 |
| `src/FoodAssistant.Client/Catalog/ICatalogClient.cs` | neu | **Vertrag für Spur A** (A implementiert): `SearchProductsAsync(Guid householdId, string? query, string? cursor, int? limit, CancellationToken)` → `ApiResult<CursorPage<ProductSummaryDto>>`; `GetProductAsync(Guid householdId, Guid productId, …)` → `ApiResult<ProductDto>`; `FindByBarcodeAsync(Guid householdId, string barcode, …)`; `CreateProductAsync(Guid householdId, CreateProductRequest, …)`; `UpdateProductAsync(Guid householdId, Guid productId, UpdateProductRequest, string etag, …)`; `ArchiveProductAsync(Guid householdId, Guid productId, string etag, …)` → `ApiResult`; `ListStorageLocationsAsync(Guid householdId, …)` → `ApiResult<IReadOnlyList<StorageLocationDto>>`; `CreateStorageLocationAsync`, `UpdateStorageLocationAsync(…, string etag, …)`, `DeleteStorageLocationAsync(…, string etag, …)`; `ListCategoriesAsync(Guid householdId, …)` → `ApiResult<IReadOnlyList<CategoryDto>>` | – | 50 |
| `src/FoodAssistant.Client/Sync/ILiveUpdates.cs` + `NoLiveUpdates.cs` + `LiveConnectionState.cs` | neu | `event Func<HouseholdChangesMessage, Task>? Changed`; `event Func<Task>? Resynced` (nach Wiederverbindung: Seiten laden neu); `LiveConnectionState State`; `event Action? StateChanged`; `Task StartAsync(Guid householdId, CancellationToken ct)`; `Task StopAsync()`; Enum `Disconnected, Connecting, Connected, Reconnecting`; Platzhalter bleibt `Disconnected` | – | 30+25+10 |
| `src/FoodAssistant.Client/Composition/ClientRegistration.cs` | neu | `AddFoodAssistantClient(this IServiceCollection services, Uri baseAddress)`: `AddTransient<AntiforgeryHandler>`, `AddTransient<TraceContextHandler>`, `AddHttpClient<ApiHttpClient>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<TraceContextHandler>().AddHttpMessageHandler<AntiforgeryHandler>()`; `TryAddScoped<IAccountClient, AccountClient>`; `TryAddScoped<ILiveUpdates, NoLiveUpdates>`; danach die Feature-Stubs | – | 50 |
| `src/FoodAssistant.Client/Composition/{Catalog,ShelfLife,Inventory,FoodFacts,Shopping,Checkout,Restock,Households,Notifications,Statistics,Labels,Import,Export,Activity}ClientRegistration.cs` | neu | Stubs `Add<Feature>Client(this IServiceCollection)` | – | 14×12 |
| `src/FoodAssistant.Client/Http/HttpLog.cs` | neu | `NetworkError` (4010, Debug, `Method`) – nie Pfad mit Query | – | 15 |
| `src/FoodAssistant.Web.Client/Program.cs` | ändern | `builder.Services.AddFoodAssistantClient(new Uri(builder.HostEnvironment.BaseAddress));` vor `AddFoodAssistantUi()` | – | +1 |
| `src/FoodAssistant.Server/Composition/UiRegistration.cs` | ändern | `AddFoodAssistantClient(<PublicUrl aus HostingOptions>)` – nur damit die DI-Prüfung der UI-Dienste auf dem Server vollständig ist (Seiten werden dort nicht mehr gerendert) | – | +5 |
| `tests/FoodAssistant.Client.Tests/FoodAssistant.Client.Tests.csproj` + `Support/FakeHttpHandler.cs` | neu | xUnit v3 wie `tests/FoodAssistant.Domain.Tests/*.csproj`; Fake-Handler mit Warteschlange von Antworten und Mitschnitt der Anfragen | – | 25+50 |
| `FoodAssistant.slnx` | ändern | Testprojekt eintragen | – | +1 |

**Akzeptanz:** Client-Tests grün; Architekturtests grün (Client kennt nur Contracts).

---

### Paket S0.5 – UI-Basis

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** S0.4 · **Muster:** Seiten wie `UI/Shell/MorePage.razor`; Texte wie
  `UI/Shell/ShellText.cs` + `.resx`/`.en.resx`; Tests wie `tests/FoodAssistant.UI.Tests/Shell/MorePageTests.cs`
  mit `Support/UiTestContext.cs`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Server/Components/App.razor` | ändern | `PageRenderMode` → `new InteractiveWebAssemblyRenderMode(prerender: false)` (Begründung als Kommentar: keine Cookie-Weitergabe beim Vorrendern) | `PagesTests` weiter grün | ±5 |
| `src/FoodAssistant.UI/Session/SessionStatus.cs` | neu | `enum SessionStatus { Loading, NeedsSetup, SignedOut, NoHousehold, Ready }` | – | 10 |
| `src/FoodAssistant.UI/Session/SessionState.cs` | neu | scoped: `MeResponse? Me`, `MeHouseholdResponse? ActiveHousehold`, `Guid? ActiveHouseholdId`, `SessionStatus Status`, `bool CanEdit` (Rolle ≥ Member), `bool IsOwner`, `event Action? Changed`; `Task LoadAsync(CancellationToken)`: `GetMeAsync`; 401 → `GetSetupStatusAsync` → `NeedsSetup`/`SignedOut`; gespeicherten Haushalt aus `IBrowserInterop.GetItemAsync("foodassistant.household")` wählen, sonst ersten; `Task SelectHouseholdAsync(Guid id)` speichert, startet `ILiveUpdates.StartAsync`; `Task SignOutAsync()` | `SessionStateTests` | 120 |
| `src/FoodAssistant.UI/Session/SessionGate.razor` | neu | umschließt `@Body`: `Loading` → `LoadingState`; öffentliche Pfade (`/login`, `/setup`, `/invite`) immer durchlassen; `SignedOut` → `/login?returnUrl=…` (nur lokale Pfade); `NeedsSetup` → `/setup`; `NoHousehold` → `EmptyState` mit Text | `SessionGateTests` | 70 |
| `src/FoodAssistant.UI/Session/LoginPage.razor` | neu | `@page "/login"`: Benutzername, Passwort (`MudTextField`, Labels), Anmelden; Fehler `accounts.invalid_credentials`/`accounts.locked_out` über `ApiErrorAlert`; Erfolg → `SessionState.LoadAsync`, Navigation zu `returnUrl` | `LoginPageTests` (Erfolg, falsches Passwort, gesperrt, Tastatur/Labels) | 90 |
| `src/FoodAssistant.UI/Session/SessionText.cs` + `.resx` + `.en.resx` | neu | Texte Anmeldung, Fehler, kein Haushalt | `ResourceCompletenessTests` | 5+2×40 |
| `src/FoodAssistant.UI/Shell/MainLayout.razor` | ändern | `@Body` in `<SessionGate>`; `<LiveRegion />` einmal im Layout | `MainLayoutTests` anpassen | ±10 |
| `src/FoodAssistant.UI/Common/CommonText.cs` + `.resx` + `.en.resx` | neu | Einheiten (`Unit.g` …), Lagertypen (`StorageType.Chilled` …), `Error.Generic`, `Error.Offline`, `Error.Network`, `Error.Conflict` (412: „Inzwischen geändert – neu geladen“), Laden/Speichern/Abbrechen/Löschen/Rückgängig | `ResourceCompletenessTests` | 5+2×60 |
| `src/FoodAssistant.UI/Common/ApiErrorAlert.razor` | neu | `[Parameter] ApiError? Error`, `[Parameter] IStringLocalizer? FeatureText`: Text `FeatureText["Error." + code]`, falls vorhanden, sonst `CommonText`; `role="alert"`; Feldfehler werden **nicht** hier, sondern am Feld gezeigt | `ApiErrorAlertTests` | 50 |
| `src/FoodAssistant.UI/Common/QuantityInput.razor` | neu | `[Parameter] QuantityDto? Value` + `ValueChanged`, `Label`, `Required`; Zahl (`MudNumericField<decimal?>`, Kultur) + Einheit (`MudSelect` über `UnitCodes.All`, Namen aus `CommonText`) | `QuantityInputTests` | 70 |
| `src/FoodAssistant.UI/Common/DateOnlyField.razor` | neu | `MudDatePicker` mit `DateOnly?`-Bindung, Label, Löschen-Knopf | `DateOnlyFieldTests` | 50 |
| `src/FoodAssistant.UI/Common/LoadingState.razor` | neu | `MudProgressCircular` + Text, `aria-busy` | – | 20 |
| `src/FoodAssistant.UI/Common/ILiveAnnouncer.cs` + `LiveAnnouncer.cs` + `LiveRegion.razor` | neu | `void Announce(string message)`; Region `aria-live="polite"` `role="status"`, visuell versteckt (Checkliste: Live-Änderungen dezent) | `LiveRegionTests` | 15+30+30 |
| `src/FoodAssistant.UI/Common/IUndoNotifier.cs` + `UndoNotifier.cs` | neu | `void Show(string message, Func<Task> undo)` über MudBlazor `ISnackbar` mit Aktion „Rückgängig“, 10 s | `UndoNotifierTests` | 15+40 |
| `src/FoodAssistant.UI/Catalog/CategoryText.cs` + `.resx` + `.en.resx` | neu | 24 Kategorienamen (S0.1-Tabelle), Schlüssel = Kategorieschlüssel | `CategoryTextTests` (UI.Tests): jeder Schlüssel aus `tests/FoodAssistant.UI.Tests/Catalog/category-keys.txt` hat DE/EN-Text; `CategoryKeysFileTests` (Server.Tests, kennt Domain): Datei == `ProductCategories.All` | 5+2×30 |
| `src/FoodAssistant.UI/Catalog/CategoryIcons.cs` | neu | `public static string For(string categoryKey)` → `Icons.Material.Rounded.*`: `dairy`/`cheese`/`plant_based`→`LocalDrink`, `eggs`→`Egg`, `meat`/`poultry`/`sausage`/`fish`→`SetMeal`, `fruit`/`vegetables`/`herbs`→`Eco`, `bread`→`BakeryDining`, `frozen`→`AcUnit`, `beverages`→`LocalBar`, `sweets`→`Cookie`, Rest→`Kitchen`; Symbol, das es in MudBlazor nicht gibt → `Kitchen` (Test: jedes Symbol ist nicht leer) | – | 45 |
| `src/FoodAssistant.UI/Scan/ScanPage.razor` + `ScanText.cs` + `.resx` + `.en.resx` | neu (verschoben) | `UI/Inventory/ScanPage.razor` hierher, Scan-Schlüssel aus `InventoryText*.resx` in `ScanText*.resx` verschieben | `PagesTests` anpassen | ±60 |
| `src/FoodAssistant.UI/Inventory/InventoryText*.resx` | ändern | Scan-Schlüssel entfernen | – | −10 |
| `src/FoodAssistant.UI/Shell/MorePage.razor` + `ShellText*.resx` | ändern | Abschnitt „Haushalt“: Haushalt & Mitglieder `/settings/household` (K), Lagerorte `/settings/locations` (A), Läden `/settings/stores` (D), Haltbarkeitsregeln `/settings/shelf-life` (B1); Abschnitt „Vorrat & Einkauf“: Produkte `/products` (A), Wiederkehrende Artikel `/shopping/recurring` (G); Abschnitt „Konto“: angemeldet als …, Haushalt wechseln (Link `/settings/household`), Abmelden (`SessionState.SignOutAsync`) | `MorePageTests` | +60 |
| `src/FoodAssistant.UI/Composition/UiServiceCollectionExtensions.cs` | ändern | `TryAddScoped<SessionState>`, `TryAddScoped<ILiveAnnouncer, LiveAnnouncer>`, `TryAddScoped<IUndoNotifier, UndoNotifier>` | – | +4 |
| `tests/FoodAssistant.UI.Tests/Support/{FakeAccountClient,FakeCatalogClient,FakeLiveUpdates}.cs` | neu | Fakes mit vorgegebenen Antworten und Aufrufprotokoll; `FakeCatalogClient` hält Produkte/Lagerorte in Listen (von A, B2, C2, D, F genutzt) | – | 60+120+40 |
| `tests/FoodAssistant.UI.Tests/Support/UiTestContext.cs` | ändern | Fakes registrieren, `SessionState` mit aktivem Test-Haushalt (`Role = Member`) vorbelegbar | – | +20 |

**Barrierefreiheit:** Checkliste `docs/kb/barrierefreiheit.md` für Login, „Mehr“, Live-Region.
**Akzeptanz:** UI-Tests grün; im Browser-Pane (`dotnet run --project src/FoodAssistant.Server`,
Datenbank per `docker compose up -d db`): Einrichtung per API (Testskript oder `/setup` ab K), Anmeldung
über `/login`, „Mehr“ zeigt Links, Abmelden funktioniert.

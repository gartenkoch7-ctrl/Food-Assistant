# Datenmodell

Server-Datenbank: PostgreSQL über EF Core (`Infrastructure/Persistence/AppDbContext.cs`). EF-Entitäten
bleiben in Infrastructure; nach außen gehen Domain-Typen bzw. Contracts-DTOs (AGENTS.md 5).
Schemaänderungen nur über Migrationen, nur vorwärts (ADR-0007).

## Konventionen

- Je Entität `Feature/<Entität>.cs` + `Feature/<Entität>Configuration.cs`
  (`internal sealed class … : IEntityTypeConfiguration<T>`); `ApplyConfigurationsFromAssembly` findet sie.
- **Keine `DbSet`-Properties** für neue Entitäten: Stores nutzen `db.Set<T>()`.
- Tabellenname im Plural, `Id` als UUIDv7 (`Guid.CreateVersion7()`, `ValueGeneratedNever()`).
- Enums als Text: `HasConversion<string>().HasMaxLength(16)`.
- Gemeinsame Spalten: `Id`, `HouseholdId` (FK → `Households`, Cascade), `Version`, `CreatedAt`, `UpdatedAt`.
- **Version/ETag:** `uint Version` + `IsRowVersion()` = PostgreSQL-`xmin`. Ausnahme
  `ShoppingItems.Version` (`long`, `IsConcurrencyToken()`): die Anwendung erhöht sie bei **jeder**
  Änderung selbst, weil die Konfliktregeln der Offline-Einkaufsliste sie brauchen (ADR-0005).
- Mengen als `…Value decimal(12,3)` + `…UnitCode` (16); Ablaufdaten als `DateOnly`.

## Haushaltstrennung und Filter

- Jede Haushaltsentität implementiert `IHouseholdOwned` und bekommt den benannten Filter
  `AppDbContext.ActiveHouseholdFilter` (aktiver Haushalt aus `IHouseholdContext`). Ohne aktiven
  Haushalt liefern Abfragen nichts.
- `AppDbContext.NotDeletedFilter` blendet gelöschte Haushalte (samt Mitgliedschaften und
  Einladungen) und soft-gelöschte Einkaufslisten-Einträge (`ISoftDeletable`) aus.
- `IgnoreQueryFilters` nur mit Kommentar, warum es sicher ist. Hintergrunddienste nutzen stattdessen
  `IHouseholdScopeFactory` (siehe unten).
- Schreiben ist nicht gefiltert: Wer anlegt, setzt `HouseholdId` selbst (aus `IHouseholdContext`).

## Entitäten (Stand M1 S0)

| Tabelle | Entität | Inhalt | Sync (`SyncEntityTypes`) |
|---|---|---|---|
| `Users`, `User*` | `User` (Identity) | Konten | – |
| `Households` | `Household` | Haushalt, Zeitzone, `ExpiryLeadDays` (Standard 2), `DeletedAt` | – |
| `HouseholdMemberships` | `HouseholdMembership` | Person ↔ Haushalt mit Rolle | – |
| `Invitations` | `Invitation` | Einladungen (nur Hash des Tokens) | – |
| `StorageLocations` | `StorageLocation` | Lagerorte mit `StorageType` | `storage_location` |
| `Products` | `Product` | Produkte, Kategorie, Standardeinheit, Mindestbestand, `ArchivedAt` | `product` |
| `ProductBarcodes` | `ProductBarcode` | Barcodes, eindeutig je Haushalt; Änderungen laufen über das Produkt (`UpdatedAt` des Produkts mit setzen) | (über `product`) |
| `StockBatches` | `StockBatchRecord` | Chargen; Domain-Typ `StockBatch` über `StockBatchMapping` | `batch` |
| `ConsumptionEvents` | `ConsumptionEvent` | Verbrauch/Wegwurf (Statistik, Rückgängig) | – |
| `ShelfLifeOverrides` | `ShelfLifeOverride` | Haltbarkeitsregeln des Haushalts je Kategorie **oder** Produkt | – |
| `ShoppingLists` | `ShoppingList` | Listen, höchstens eine Standardliste je Haushalt | `shopping_list` |
| `ShoppingItems` | `ShoppingItem` | Einträge, eigene `long Version`, Soft-Delete (`DeletedAt`) | `shopping_item` |
| `RecurringShoppingRules` | `RecurringShoppingRule` | Wiederkehrende Einträge | – |
| `Stores` | `Store` | Läden | `store` |
| `StoreSectionOrders`, `StoreSectionTransitions` | … | Bereichsreihenfolge je Laden, gelernte Übergänge | – |
| `ChangeLog` | `ChangeLogEntry` | Änderungsprotokoll für Sync (nur vom Interceptor geschrieben) | – |
| `HouseholdActivities` | `HouseholdActivity` | Aktivitätsverlauf, Daten für Rückgängig | – |
| `FoodFactsCache` | `FoodFactsCacheEntry` | **Serverweiter** Cache von Open Food Facts (kein Haushaltsbezug) | – |

Migrationen: `InitialAccounts` (M0), `M1Core` (M1 S0.1). Weitere sind im Meilensteinplan je Spur vergeben.

## Änderungsprotokoll und Live-Updates (ADR-0005)

Entitäten mit `ISyncTracked` werden synchronisiert; Features markieren sie nur, mehr nicht.
`SyncChangeInterceptor` (+ `SyncTransactionInterceptor`) schreibt bei jedem `SaveChangesAsync` für
jede hinzugefügte, geänderte oder gelöschte Entität einen `ChangeLogEntry` in **derselben**
Transaktion (`Deleted` = gelöscht oder `DeletedAt` gesetzt) und meldet die Änderungen nach dem
Commit an `IHouseholdChangeNotifier` (Server: SignalR-Gruppe des Haushalts). Nach einem Rollback
wird nichts gemeldet; schlägt die Meldung fehl, bleibt die Speicherung gültig (Log 2200).

**Reihenfolge der `Sequence`:** Identity-Werte werden beim Einfügen vergeben, nicht beim Commit.
Ohne Sperre könnte ein Schreiber mit kleinerer Sequenz nach einem mit größerer committen; ein Client,
der „Änderungen nach N“ liest, würde die kleinere für immer überspringen. Deshalb nimmt jeder
Schreibvorgang mit Protokolleinträgen eine **Advisory-Sperre je Haushalt** bis zum Ende seiner
Transaktion (`PostgresLocks.LockHouseholdChangeLogAsync`). Folge: Innerhalb eines Haushalts werden
Sequenzen streng aufsteigend sichtbar; „`Sequence > since`“ ist ein sicheres Lesen. Schreiber
verschiedener Haushalte warten nicht aufeinander.

Regeln für Stores und Dienste:
- Synchronisierte Entitäten nur mit **`SaveChangesAsync`** speichern (synchrones `SaveChanges` wirft
  `NotSupportedException`), nicht mit `acceptAllChangesOnSuccess: false`.
- Nur in EF-Core-Transaktionen: kein umgebender `TransactionScope`, kein
  `AutoTransactionBehavior.Never` (beides wirft `NotSupportedException`), kein Commit am
  rohen `DbTransaction` vorbei. Eine ohne Commit verworfene Transaktion wird nie gemeldet.
- **Kein `ExecuteUpdate`/`ExecuteDelete`** auf synchronisierten Entitäten: das umgeht den Interceptor
  und damit Protokoll und Live-Update. Ausnahme nur mit eigenem Protokolleintrag und Kommentar.
- **Kaskaden in der Datenbank** umgehen den Interceptor ebenso: `ON DELETE CASCADE`/`SET NULL` auf
  synchronisierte Entitäten (`ShoppingItems.ListId`, `ShoppingItems.ProductId`,
  `Products.DefaultStorageLocationId`, `ShoppingLists.StoreId`) ändern Zeilen ohne Protokolleintrag.
  Wer die Elternzeile löscht, lädt die betroffenen Kinder vorher und ändert bzw. löscht sie über den
  Context (dann schreibt EF Core die Änderung selbst und der Interceptor sieht sie), oder archiviert
  statt zu löschen.
- Eigene Savepoints: wird auf einen Savepoint zurückgerollt, meldet der Commit trotzdem die
  Änderungen danach (die Sequenzen fehlen dann im Protokoll). Clients laden ohnehin über das
  Protokoll nach; trotzdem keine eigenen Savepoints verwenden.
- Die Identity-Sequenz von `ChangeLog.Sequence` muss ohne Cache laufen (`CACHE 1`, Standard;
  Test `ChangeLogSequence_HandsOutValuesWithoutCache`), sonst ordnet die Sperre nichts.
- Lange Transaktionen (Import) halten die Sperre des Haushalts; andere Schreiber desselben Haushalts
  warten so lange. In kleinen Transaktionen arbeiten.
- Ein Speichervorgang, der mehrere Haushalte schreibt, sperrt sie in fester Reihenfolge (sortierte
  Ids). Über mehrere Speichervorgänge einer Transaktion gilt das nicht; dort möglichst nur einen
  Haushalt schreiben (sonst kann PostgreSQL eine Verklemmung erkennen und eine Transaktion abbrechen).

## Store-Muster (verbindlich ab M1)

Endpunkt liest `If-Match` mit `IfMatch.TryRead` (428 wenn Pflicht und fehlt, `*` = ohne Prüfung),
der Anwendungsfall liefert `OperationResult<T>`, der Endpunkt antwortet mit `result.ToHttpResult(http, dto => dto.Version)`.
Im Store:

1. Laden mit Haushaltsfilter; nicht gefunden → `NotFound(code)`.
2. `expectedVersion` (aus `IfMatch.TryRead`) ≠ `entity.Version` → `VersionMismatch(entity.Version, dto)`.
3. Vor dem Speichern `db.Entry(entity).Property(e => e.Version).OriginalValue = (uint)expected`, damit ein
   Wettlauf zwischen Laden und Speichern als `DbUpdateConcurrencyException` auffällt → neu laden,
   `VersionMismatch`.
4. Nach dem erfolgreichen Speichern `IStockChangedHandler` bzw. andere Hooks aufrufen.

## Gemeinsame Nahtstellen (M1 S0.2)

| Nahtstelle | Zweck |
|---|---|
| `IHouseholdCalendar` | „Heute“ in der Zeitzone des Haushalts + `ExpiryLeadDays`; Einstellungen einmal je Scope gelesen, Datum bei jedem Aufruf berechnet |
| `IHouseholdScopeFactory` (Singleton) | Scope für Hintergrunddienste mit gesetztem Haushalt; Person = `HouseholdScopes.SystemUserId`, Rolle Besitzer. Haushalte: `IHouseholdStore.ListActiveHouseholdIdsAsync` |
| `IActivityLog.Add` | Verlaufseintrag, gespeichert mit dem nächsten `SaveChangesAsync` desselben Scopes; System-Person wird als `null` gespeichert |
| `IStockChangedHandler` | Hook nach Bestandsänderung; Aufrufer rufen **alle** Handler nach dem Speichern, Fehler werden geloggt |
| `IShelfLifeRules` | Haltbarkeitsregeln; Platzhalter `NoShelfLifeRules` liefert `ShelfLifeRule.Empty` (kein Datum) bis B1 |

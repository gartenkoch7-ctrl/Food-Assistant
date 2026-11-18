# M1 D – Einkaufslisten, Läden, gelernte Reihenfolge

Mehrere Listen (eine Standardliste), Einträge mit Produkt oder Freitext, Abhaken, Konfliktregeln
für die spätere Offline-Warteschlange (ADR-0005, `ShoppingConflictResolver` aus M0), Läden mit
Bereichsreihenfolge (F3), die aus dem Abhaken gelernt wird und manuell Vorrang hat.
KB: [einkauf](../../kb/einkauf.md), ADR-0005.

Branch `feat/m01-d-shopping` · Pakete nacheinander: D1 → D2 → D3 (D3 erst nach Merge A).
Eine Spur, weil D2 und D3 Dateien von D1 erweitern (§11).

## Aufgaben
- [x] D1 Listen und Einträge (Server) mit Konfliktregeln
- [ ] D2 Läden, Bereiche, gelernte und manuelle Reihenfolge (Domain + Server)
- [ ] D3 Client und UI: Listen, Liste mit Bereichen, Abhaken, Läden-Seite

## Fachregeln (verbindlich)

- **Standardliste:** Gibt es beim Lesen der Listen keine, wird eine mit leerem `Name` und
  `IsDefault = true` angelegt (UI zeigt dann „Einkaufsliste“/„Shopping list“). Genau eine
  Standardliste (Unique-Index). Eine andere Liste zur Standardliste machen setzt die alte zurück
  (eine Transaktion). Standardliste löschen → 409 `shopping.default_list`.
- **Eintrag anlegen** (auch Eingeschränkt): `Id` aus Anfrage (UUIDv7) oder neu; existiert die Id im
  Haushalt schon (auch gelöscht) → **200 mit bestehendem Eintrag** (idempotent für die Warteschlange).
  Produkt → `Text` = Produktname, `SectionKey` = `Product.SectionKey ?? StoreSections.ForCategory(category)`;
  Freitext → `Text` 1–200, `SectionKey` aus Anfrage oder `other`. `Origin = Manual` (andere Herkünfte nur intern).
- **Eintrag ändern** = `PUT /shopping/items/{id}` mit **vollem Zielzustand** und `BaseVersion`
  (kein If-Match, Ausnahme laut ADR-0004): Server baut `incoming = ShoppingItemState(Id, BaseVersion, Checked, Quantity, Note, Deleted: false, UpdatedAt: now)`
  und `server = Zustand aus DB (Deleted = DeletedAt != null)`, ruft `ShoppingConflictResolver.Resolve`.
  `ServerChanged` → Werte übernehmen (`Text`, `SectionKey` folgen **letzte Änderung gewinnt** wie
  Menge/Notiz, d. h. nur wenn der eingehende Zustand gewinnt), `Version = resolution.State.Version`,
  `UpdatedAt = now`, `DeletedAt = null` falls Änderung gegen Löschung gewinnt; `Checked` false→true
  setzt `CheckedAt = now`, `CheckedByUserId`, true→false löscht beides. `BaseVersion > Version` → 409
  `shopping.unknown_version`. Antwort immer `ShoppingItemChangeResult(Item, Outcome)`.
- **Löschen** = `DELETE /shopping/items/{id}?baseVersion=n` → Resolver mit `Deleted = true`; gewinnt
  die Löschung → `DeletedAt = now`, `Version + 1`; sonst bleibt der Eintrag (Ergebnis zeigt Outcome).
- **Eingeschränkt** darf anlegen und `PUT`, wenn sich gegenüber dem **aktuellen** Serverzustand nur
  `Checked` unterscheidet; sonst 403 `shopping.restricted_change`. Löschen, Listen, Läden: Member+.
- **Gelernte Reihenfolge (D2):** Beim Abhaken (false→true) eines Eintrags auf einer Liste mit Laden:
  `Store.LastCheckedAt` liegt ≤ 2 h zurück **und** `LastCheckedSectionKey` ≠ aktueller Bereich →
  Übergang `(Last → aktuell)` +1. Dann `LastCheckedSectionKey/At` setzen. Reihenfolge:
  `SectionOrderLearner.Order` (unten). Manuelle Reihenfolge vorhanden → sie gilt für die genannten
  Bereiche, übrige Bereiche danach in gelernter Reihenfolge.
- **Verlauf:** `IActivityLog` (Area `Shopping`) für Eintrag angelegt, abgehakt, gelöscht und für
  jede Konfliktauflösung mit Outcome ≠ `Created`/`NoConflict` (Payload: Outcome).
- **Live:** alles über den Sync-Interceptor (S0.2); nichts extra.

## Detailplan

### Paket D1 – Listen und Einträge

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Schritt 0 · **Blockiert durch:** – (F3 Annahme a)
- **Muster:** wie A1 (`m01-a-katalog.md`); Store-Muster `docs/kb/datenmodell.md`; Resolver
  `src/FoodAssistant.Domain/Shopping/ShoppingConflictResolver.cs` und dessen Tests.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Shopping/ShoppingListDto.cs`, `CreateShoppingListRequest.cs`, `UpdateShoppingListRequest.cs` | neu | `ShoppingListDto(Guid Id, string Name, Guid? StoreId, bool IsDefault, int SortOrder, int OpenItemCount, int CheckedItemCount, long Version)`; `CreateShoppingListRequest(Guid? Id, string Name, Guid? StoreId)`; `UpdateShoppingListRequest(string Name, Guid? StoreId, bool IsDefault, int SortOrder)` | – | 3×12 |
| `src/FoodAssistant.Contracts/Shopping/ShoppingItemDto.cs`, `CreateShoppingItemRequest.cs`, `ChangeShoppingItemRequest.cs`, `MoveShoppingItemRequest.cs`, `ShoppingItemChangeResult.cs`, `ShoppingConflictOutcomeDto.cs`, `ShoppingErrorCodes.cs` | neu | `ShoppingItemDto(Guid Id, Guid ListId, Guid? ProductId, string Text, QuantityDto? Quantity, string? Note, string SectionKey, ShoppingItemOriginDto Origin, bool Checked, DateTimeOffset? CheckedAt, long Version, DateTimeOffset UpdatedAt)`; `CreateShoppingItemRequest(Guid? Id, Guid? ProductId, string? Text, QuantityDto? Quantity, string? Note, string? SectionKey)`; `ChangeShoppingItemRequest(long BaseVersion, bool Checked, QuantityDto? Quantity, string? Note, string Text, string SectionKey)`; `MoveShoppingItemRequest(Guid ListId)`; `ShoppingItemChangeResult(ShoppingItemDto Item, ShoppingConflictOutcomeDto Outcome)`; Enum-Spiegel von `ShoppingConflictOutcome`; Codes `shopping.list_not_found`, `shopping.item_not_found`, `shopping.validation_failed`, `shopping.default_list`, `shopping.unknown_version`, `shopping.restricted_change`, `catalog.product_not_found` | – | 7×15 |
| `src/FoodAssistant.Application/Shopping/IShoppingStore.cs` | neu | `ListListsAsync`, `GetListAsync`, `AddListAsync`, `UpdateListAsync(Guid id, long? expectedVersion, …)`, `DeleteListAsync(…)`, `EnsureDefaultListAsync(…)` → `ShoppingListDto`; `ListItemsAsync(Guid listId, bool includeChecked, …)`; `GetItemAsync(Guid id, bool includeDeleted, …)` → `ShoppingItemRecord?` (Application-Record mit allen Feldern); `AddItemAsync(NewShoppingItem, …)`; `ApplyItemAsync(ShoppingItemRecord updated, long expectedVersion, …)` (Versionsprüfung über `long Version` als Concurrency-Token → `DbUpdateConcurrencyException` → Neuversuch einmal mit frischem Zustand im Service); `MoveItemAsync`; `GetProductForItemAsync(Guid productId, …)` → `(string Name, string CategoryKey, string? SectionKey)?`; `GetStoreIdOfListAsync(Guid listId, …)` | – | 50 |
| `src/FoodAssistant.Application/Shopping/{ShoppingItemRecord,NewShoppingItem}.cs` | neu | Records | – | 2×20 |
| `src/FoodAssistant.Application/Shopping/ShoppingListService.cs` | neu | Listen-CRUD + Standardliste nach Regeln; **`public Task<ShoppingListDto> EnsureDefaultListAsync(Guid householdId, CancellationToken)`** (von G genutzt) | über Endpunkt-Tests | 150 |
| `src/FoodAssistant.Application/Shopping/ShoppingItemService.cs` | neu | `AddAsync(Guid householdId, Guid listId, CreateShoppingItemRequest, Guid userId, HouseholdRole role, …)`, `ChangeAsync(…, Guid itemId, ChangeShoppingItemRequest, …)`, `DeleteAsync(…, Guid itemId, long baseVersion, …)`, `MoveAsync(…)`; Resolver-Ablauf laut Regeln; nach Abhaken `ISectionOrderLearning.RecordCheckAsync(storeId, sectionKey, now)` (Interface in D1 angelegt, Platzhalter-Implementierung bis D2) ; **`public Task<ShoppingItemDto> AddInternalAsync(Guid householdId, Guid listId, NewShoppingItem item, …)`** für G (Herkunft MinimumStock/Recurring) | über Endpunkt-Tests | 230 |
| `src/FoodAssistant.Application/Shopping/ShoppingMapping.cs` | neu | `public static ShoppingItemDto ToDto(ShoppingItemRecord r)`; Outcome-Abbildung – öffentlich, E2 nutzt es | – | 40 |
| `src/FoodAssistant.Application/Shopping/ShoppingValidation.cs` | neu | Name ≤ 80, Text 1–200, Notiz ≤ 500, Menge > 0 und Einheit gültig, `SectionKey` ∈ `StoreSections.All` | `ShoppingValidationTests` | 70 |
| `src/FoodAssistant.Application/Shopping/ShoppingLog.cs` | neu | `ShoppingConflictResolved` (3400, Information, `ItemId`, `Outcome`), `ShoppingListCreated` (3401, `ListId`), `ShoppingListDeleted` (3402), `RestrictedChangeDenied` (3403, Information, –) | – | 30 |
| `src/FoodAssistant.Application/Shopping/ShoppingMetrics.cs` | neu | `foodassistant.shopping.conflicts` (Counter `{conflict}`, `outcome`), `foodassistant.shopping.items.checked` (Counter `{item}`) | – | 25 |
| `src/FoodAssistant.Application/Stores/ISectionOrderLearning.cs` + `NoSectionOrderLearning.cs` | neu | `Task RecordCheckAsync(Guid storeId, string sectionKey, DateTimeOffset checkedAt, CancellationToken)`; Platzhalter (D2 ersetzt) | – | 15+12 |
| `src/FoodAssistant.Application/Composition/ShoppingRegistration.cs` | ändern (Stub) | Services, `TryAddScoped<ISectionOrderLearning, NoSectionOrderLearning>` | – | +5 |
| `src/FoodAssistant.Infrastructure/Shopping/ShoppingStore.cs` | neu | EF; `NotDeletedFilter` nur in `GetItemAsync(includeDeleted: true)` per `IgnoreQueryFilters([AppDbContext.NotDeletedFilter])` (Haushaltsfilter bleibt! Kommentar); `long Version` als Concurrency-Token setzen: `entry.Property(i => i.Version).OriginalValue = expectedVersion` | `ShoppingStoreTests` (Testcontainers): Standardliste eindeutig unter Parallelität (zwei Scopes), Soft-Delete, Concurrency | 260 |
| `src/FoodAssistant.Infrastructure/Composition/ShoppingInfrastructureRegistration.cs` | ändern (Stub) | `AddScoped<IShoppingStore, ShoppingStore>()` | – | +1 |
| `src/FoodAssistant.Server/Shopping/ShoppingEndpoints.cs` | ändern (Stub) | Tag `Shopping`: `GET /shopping/lists` (Any); `POST /shopping/lists` (Member); `PUT /shopping/lists/{listId:guid}` (Member, If-Match Pflicht); `DELETE /shopping/lists/{listId:guid}` (Member, If-Match Pflicht); `GET /shopping/lists/{listId:guid}/items?includeChecked=true` (Any); `POST /shopping/lists/{listId:guid}/items` (Any; 201 bzw. 200 bei bekannter Id); `PUT /shopping/items/{itemId:guid}` (Any, Rolle prüfen im Service); `DELETE /shopping/items/{itemId:guid}?baseVersion=` (Member); `POST /shopping/items/{itemId:guid}/move` (Member). Person/Rolle aus `HttpHouseholdContext.Membership` | – | 170 |
| `tests/FoodAssistant.Server.Tests/Shopping/ShoppingListEndpointTests.cs` | neu | Standardliste automatisch, Standard wechseln, Standard löschen 409, CRUD, fremder Haushalt 404, Eingeschränkt 403 bei Listen | ja | 200 |
| `tests/FoodAssistant.Server.Tests/Shopping/ShoppingItemEndpointTests.cs` | neu | Anlegen Produkt/Freitext, idempotente Id, Bereich aus Produkt/Kategorie, Abhaken setzt `CheckedAt`, Eingeschränkt darf nur abhaken (403 sonst), Verschieben, fremder Haushalt 404 (auch Liste eines anderen Haushalts als Ziel) | ja | 240 |
| `tests/FoodAssistant.Server.Tests/Shopping/ShoppingConflictEndpointTests.cs` | neu | **jede Zeile der Konflikttabelle ADR-0005** über die API mit veralteter `BaseVersion`: Abhaken vs. Änderung, Menge zweimal (letzte gewinnt), gelöscht vs. geändert, gelöscht vs. abgehakt, unbekannte Version 409 | ja | 220 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md` | ändern | Snapshot; Abläufe `foodassistant.shopping.create_list`, `.update_list`, `.delete_list`, `.add_item`, `.change_item`, `.delete_item`, `.move_item` | – | – |

**Akzeptanz:** Tests grün, Konflikttabelle vollständig als API-Tests; `docs/kb/einkauf.md` um
„Standardliste ohne Namen“, „Eingeschränkt: nur abhaken/ergänzen“ ergänzt.

### Paket D2 – Läden und Reihenfolge

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** D1 · **Muster:** Domain wie `src/FoodAssistant.Domain/Inventory/MinimumStockPolicy.cs`; Rest wie D1.

**Algorithmus `SectionOrderLearner.Order(IEnumerable<SectionTransition> transitions, IReadOnlyDictionary<string,int> manualPositions)`:**
1. Punktzahl je Bereich `score(s) = Σ count(x→s) − Σ count(s→x)` (spät abgehakte Bereiche haben hohe Werte).
2. Gelernte Reihenfolge: alle `StoreSections.All` aufsteigend nach `score`, bei Gleichstand Standardreihenfolge.
3. Manuelle Positionen: diese Bereiche zuerst nach `Position`, danach die übrigen in gelernter Reihenfolge.
`static bool IsSameTrip(DateTimeOffset? lastCheckAt, DateTimeOffset now) => lastCheckAt is { } l && now - l <= TimeSpan.FromHours(2) && now >= l;`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Shopping/SectionTransition.cs` + `SectionOrderLearner.cs` | neu | Record `(string From, string To, int Count)`; Algorithmus oben | `SectionOrderLearnerTests`: ohne Daten = Standard, ein Einkauf, widersprüchliche Einkäufe, manuell teilweise, unbekannte Schlüssel ignoriert | 15+70 |
| `src/FoodAssistant.Contracts/Stores/StoreDto.cs`, `StoreRequest.cs`, `SectionOrderDto.cs`, `SetSectionOrderRequest.cs`, `StoreErrorCodes.cs` | neu | `StoreDto(Guid Id, string Name, long Version)`; `StoreRequest(Guid? Id, string Name)`; `SectionOrderDto(Guid? StoreId, IReadOnlyList<string> Sections, bool HasManualOrder)`; `SetSectionOrderRequest(IReadOnlyList<string> Sections)`; Codes `stores.store_not_found`, `stores.validation_failed` | – | 5×10 |
| `src/FoodAssistant.Application/Stores/IStoreStore.cs` | neu | Laden-CRUD; `GetTransitionsAsync(Guid storeId)`; `GetManualOrderAsync`; `SetManualOrderAsync`; `ClearManualOrderAsync`; `RecordTransitionAsync(Guid storeId, string? from, string to, DateTimeOffset at)` (setzt `LastChecked*`, erhöht Zähler per Upsert) | – | 30 |
| `src/FoodAssistant.Application/Stores/StoreService.cs` | neu | CRUD (Member), `GetSectionOrderAsync(Guid householdId, Guid? storeId)` (ohne Laden → Standardreihenfolge), `SetSectionOrderAsync` (alle Schlüssel bekannt, keine Dubletten), `ResetSectionOrderAsync` | über Endpunkt-Tests | 140 |
| `src/FoodAssistant.Application/Stores/SectionOrderLearning.cs` | neu | implementiert `ISectionOrderLearning` mit `IsSameTrip`-Regel | `SectionOrderLearningTests` | 50 |
| `src/FoodAssistant.Application/Stores/StoresLog.cs` | neu | `StoreCreated` (3450), `StoreDeleted` (3451), `SectionOrderSet` (3452, `StoreId`), `SectionOrderReset` (3453) | – | 25 |
| `src/FoodAssistant.Application/Composition/ShoppingRegistration.cs` | ändern | `AddScoped<ISectionOrderLearning, SectionOrderLearning>()` (ersetzt Platzhalter), `AddScoped<StoreService>()` | – | +2 |
| `src/FoodAssistant.Infrastructure/Stores/StoreStore.cs` | neu | EF; Upsert Übergänge mit `ExecuteUpdateAsync`/Insert bei 0 Zeilen (Unique-PK-Konflikt → einmal wiederholen) | `StoreStoreTests` | 160 |
| `src/FoodAssistant.Infrastructure/Composition/ShoppingInfrastructureRegistration.cs` | ändern | `AddScoped<IStoreStore, StoreStore>()` | – | +1 |
| `src/FoodAssistant.Server/Stores/StoreEndpoints.cs` | ändern (Stub) | Tag `Stores`: `GET /stores` (Any), `POST /stores` (Member), `PUT/DELETE /stores/{storeId:guid}` (Member, If-Match), `GET /shopping/sections` (Any, Standardreihenfolge), `GET /stores/{storeId:guid}/section-order` (Any), `PUT …/section-order` (Member), `DELETE …/section-order` (Member) | – | 110 |
| `tests/FoodAssistant.Server.Tests/Stores/StoreEndpointTests.cs`, `SectionOrderLearningTests.cs` | neu | CRUD, Rechte, fremder Haushalt; Abhaken über D1-API in Folge (Obst → Kühlregal → Getränke) ändert Reihenfolge; > 2 h Pause zählt nicht; manuell gewinnt | ja | 180+150 |

**Akzeptanz:** Tests grün; Snapshot; Telemetrie-Abläufe `foodassistant.stores.*`.

### Paket D3 – Client und UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** D2 **und Merge A** · **Muster:** wie A2/B2.3; `UI/Common/ListRow.razor` (Abhaken-Knopf im `End`-Slot, ≥ 48 px).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Shopping/IShoppingClient.cs` + `ShoppingClient.cs` | neu | alle D1-Endpunkte; **Interface ist die Offline-Nahtstelle für M2** (Doku-Kommentar) | `ShoppingClientTests` | 40+140 |
| `src/FoodAssistant.Client/Stores/IStoreClient.cs` + `StoreClient.cs` | neu | alle D2-Endpunkte | `StoreClientTests` | 25+80 |
| `src/FoodAssistant.Client/Composition/ShoppingClientRegistration.cs` | ändern (Stub) | beide registrieren | – | +2 |
| `src/FoodAssistant.UI/Shopping/SectionText.cs` + `.resx` + `.en.resx` | neu | 11 Bereichsnamen | `ResourceCompletenessTests` | 5+2×15 |
| `src/FoodAssistant.UI/Shopping/ShoppingPage.razor` | ändern (ersetzt Platzhalter) | `@page "/shopping"`: genau eine Liste → direkt `/shopping/{id}`; sonst Übersicht (Name bzw. „Einkaufsliste“, Laden, offene Anzahl), Liste anlegen (Member) | `ShoppingPageTests` | 110 |
| `src/FoodAssistant.UI/Shopping/ShoppingListPage.razor` | neu | `@page "/shopping/{ListId:guid}"`: Schnell-Eingabe oben (Freitext, Enter = anlegen; Knopf „Produkt wählen“ → `ProductPicker`), Einträge nach Bereich gruppiert in der Reihenfolge von `GetSectionOrderAsync(list.StoreId)` (Überschriften = Bereichsnamen), Abhaken-Knopf je Zeile (zugänglicher Name „{Text} abhaken“), abgehakte unten zusammengeklappt „Im Wagen (n)“, Knopf **„Einkauf abschließen“** (nur Member+, nur wenn abgehakte vorhanden) → `/shopping/{ListId}/complete` (Seite von F); doppelte Produkte → Hinweis „zusammenführen?“ (Mengen gleicher Einheit addieren: ein `PUT`, ein `DELETE`); Live: `Changed` mit `shopping_item`/`shopping_list` → neu laden, fremde Abhaken ansagen | `ShoppingListPageTests` | 240 |
| `src/FoodAssistant.UI/Shopping/ShoppingItemRow.razor` | neu | eine Zeile (Text, Menge, Notiz, Herkunft „automatisch“ bei MinimumStock/Recurring als Text), Abhaken optimistisch mit Rücksetzen bei Fehler | `ShoppingItemRowTests` | 90 |
| `src/FoodAssistant.UI/Shopping/ShoppingItemDialog.razor` | neu | Menge, Notiz, Bereich, Liste wechseln, Löschen (Member+) | `ShoppingItemDialogTests` | 120 |
| `src/FoodAssistant.UI/Shopping/ShoppingListDialog.razor` | neu | Name, Laden, „Standardliste“ | – | 80 |
| `src/FoodAssistant.UI/Stores/StoresPage.razor` + `StoreDialog.razor` + `SectionOrderEditor.razor` | neu | `@page "/settings/stores"`: Läden-CRUD; Reihenfolge-Editor mit **Auf/Ab-Knöpfen** (kein reines Ziehen), „Gelernt“/„Eigene Reihenfolge“ als Text, „Zurück zur gelernten“ | `StoresPageTests`, `SectionOrderEditorTests` | 130+80+120 |
| `src/FoodAssistant.UI/Shopping/ShoppingText*.resx`, `src/FoodAssistant.UI/Stores/StoresText.cs` + resx | ändern/neu | Texte, `Error.shopping.*`, `Error.stores.*` | – | – |
| `tests/FoodAssistant.UI.Tests/Shopping/*`, `Stores/*` + `Support/FakeShoppingClient.cs`, `FakeStoreClient.cs` | neu | bUnit | ja | 7×110+2×100 |

**Barrierefreiheit:** Checkliste; Abhaken per Tastatur; Gruppen mit Überschriften; Live-Ansagen dezent.
**Akzeptanz:** Tests grün; Sichtprüfung mit zwei Browsern (nach Merge E1): Abhaken erscheint im anderen.

# M1 A – Produktkatalog und Lagerorte

Produkte (Katalog pro Haushalt) mit Barcodes, fester Kategorie (F1), Standard-Einheit,
Standard-Lagerort, optionalem Ladenbereich und Mindestbestand; Lagerorte mit Typ. Grundlage für
Chargen (B2), Scan (C2), Einkauf (D, F) und Nachkauf (G). KB: [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md), ADR-0010.

Branch `feat/m01-a-catalog` · Pakete nacheinander: A1 → A2.

## Aufgaben
- [x] A1 Server: Produkte, Barcodes, Kategorien-Liste, Lagerorte (Application, Infrastructure, Endpunkte, Tests)
- [x] A2 Client und UI: `CatalogClient`, Produktliste, Produktdialog, `ProductPicker`, Lagerorte-Seite

## Detailplan

### Paket A1 – Server

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Blockiert durch:** –
- **Muster:** Service `Application/Households/MemberService.cs`; Store `Infrastructure/Households/InvitationStore.cs`;
  Endpunkte `Server/Households/MemberEndpoints.cs`; Ergebnisabbildung `Server/Api/Results/OperationResults.cs` (S0.2);
  Versionen nach „Muster für Stores“ in `docs/kb/datenmodell.md` (S0.2); Pagination `Server/Api/Pagination/CursorPagination.cs`;
  Tests `tests/FoodAssistant.Server.Tests/Auth/MemberTests.cs` + `Support/HouseholdDataSeed.cs` (S0.1).

**Regeln:**
- Lesen: Policy `HouseholdAny`; Schreiben: `HouseholdMember` (Eingeschränkt → 403).
- `Name` getrimmt, 1–120 Zeichen; `Brand` ≤ 120; `CategoryKey` ∈ `ProductCategories.All`;
  `DefaultUnit` ∈ `Unit.All` (`Unit.TryFromCode`); `SectionKey` null oder ∈ `StoreSections.All`;
  `MinimumStock`: Wert > 0, Einheit gültig; Barcodes: nur Ziffern, Länge 8, 12, 13 oder 14, je Haushalt eindeutig
  (Konflikt → 409 `catalog.barcode_in_use`), max. 10 pro Produkt.
- Produkt-Id aus Anfrage (UUIDv7) oder `Guid.CreateVersion7()`; existiert sie schon → 409 `catalog.product_exists`.
- Barcodes ändern → Produkt `UpdatedAt` setzen (Sync-Regel S0.1).
- Archivieren (`DELETE`) nur ohne Chargen (sonst 409 `catalog.product_has_stock`); archivierte
  Produkte sind in Suche, Barcode-Suche und `GET` nicht sichtbar (404).
- **Mindestbestand:** Nach Anlegen/Ändern mit geändertem `MinimumStock` alle `IStockChangedHandler`
  aufrufen (Spur G reagiert); vorher `MinimumStockTriggered = false` setzen, wenn der Wert sich ändert.
- Lagerort: `Name` 1–80; `Type` änderbar nur ohne Chargen am Ort (409 `catalog.location_type_locked`);
  Löschen nur ohne Chargen (409 `catalog.location_in_use`), setzt `DefaultStorageLocationId` betroffener Produkte auf null.
- Suche: `query` (optional, ≤ 100) als `EF.Functions.ILike(Name, "%q%")` **oder** exakter Barcode;
  Sortierung `Name`, `Id`; Cursor-Position `record ProductCursor(string Name, Guid Id)`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/Catalog/ICatalogStore.cs` | neu | `SearchAsync(string? query, ProductCursor? after, int take, CancellationToken)` → `IReadOnlyList<ProductSummaryDto>`; `GetProductAsync(Guid id, …)` → `ProductDto?`; `FindByBarcodeAsync(string code, …)`; `AddProductAsync(NewProduct, …)` → `OperationResult<ProductDto>`; `UpdateProductAsync(Guid id, long? expectedVersion, ProductChanges, …)`; `ArchiveProductAsync(Guid id, long? expectedVersion, DateTimeOffset now, …)`; `ListLocationsAsync`, `AddLocationAsync`, `UpdateLocationAsync`, `DeleteLocationAsync` (je `OperationResult<…>`) | – | 45 |
| `src/FoodAssistant.Application/Catalog/ProductCursor.cs`, `NewProduct.cs`, `ProductChanges.cs` | neu | Records (Werte bereits validiert, `Quantity?` statt DTO) | – | 3×15 |
| `src/FoodAssistant.Application/Catalog/CatalogValidation.cs` | neu | `static IReadOnlyDictionary<string,string[]> ValidateProduct(string name, string? brand, string categoryKey, string unit, string? sectionKey, QuantityDto? minimum, IReadOnlyList<string>? barcodes)`; `ValidateLocation(string name)`; `static bool IsBarcode(string)`; Feldnamen camelCase wie `HouseholdValidation.cs` | `CatalogValidationTests` (Application.Tests) | 110 |
| `src/FoodAssistant.Application/Catalog/ProductService.cs` | neu | `ProductService(ICatalogStore store, IHouseholdContext household, IEnumerable<IStockChangedHandler> stockHandlers, TimeProvider time, ILogger<ProductService> logger)`: `SearchAsync(Guid householdId, string? query, string? cursor, int? limit, …)` → `OperationResult<CursorPage<ProductSummaryDto>>` (ungültiger Cursor → `Invalid` mit `common.cursor_invalid`), `GetAsync`, `FindByBarcodeAsync`, `CreateAsync(Guid householdId, CreateProductRequest, …)`, `UpdateAsync(Guid householdId, Guid productId, long? expectedVersion, UpdateProductRequest, …)`, `ArchiveAsync(…)`; jeweils `household.EnsureActive`, Ablauf mit Activity und Log | über Endpunkt-Tests | 170 |
| `src/FoodAssistant.Application/Catalog/StorageLocationService.cs` | neu | `ListAsync`, `CreateAsync(Guid householdId, StorageLocationRequest, …)`, `UpdateAsync(…, long? expectedVersion, …)`, `DeleteAsync(…, long? expectedVersion, …)` | über Endpunkt-Tests | 110 |
| `src/FoodAssistant.Application/Catalog/CategoryCatalog.cs` | neu | `static IReadOnlyList<CategoryDto> All` aus `ProductCategories` + `StoreSections.ForCategory` + Lagertyp | `CategoryCatalogTests` | 30 |
| `src/FoodAssistant.Application/Catalog/CatalogLog.cs` | neu | `ProductCreated` (3200, Information, `ProductId`, `HouseholdId`), `ProductArchived` (3201), `StorageLocationCreated` (3202, `LocationId`, `HouseholdId`), `StorageLocationDeleted` (3203), `StockHandlerFailed` (3204, Warning, `ProductId`, Exception) | – | 35 |
| `src/FoodAssistant.Application/Composition/CatalogRegistration.cs` | ändern (Stub) | `AddScoped<ProductService>()`, `AddScoped<StorageLocationService>()` | – | +2 |
| `src/FoodAssistant.Infrastructure/Catalog/CatalogStore.cs` | neu | EF-Implementierung über `db.Set<Product>()` usw.; Unique-Verletzung Barcode über `PostgresErrors.IsUniqueViolation` → `Conflict(catalog.barcode_in_use)`; Löschen Lagerort prüft `db.Set<StockBatchRecord>().AnyAsync(b => b.StorageLocationId == id)` | `CatalogStoreTests` (Infrastructure.Tests, Testcontainers): Suche ILIKE, Cursor, Barcode eindeutig pro Haushalt, gleicher Barcode in zwei Haushalten erlaubt | 230 |
| `src/FoodAssistant.Infrastructure/Catalog/CatalogMapping.cs` | neu | `internal static class`: `Expression<Func<Product, ProductSummaryDto>> ToSummary`, `ProductDto ToDto(Product p)` (Barcodes sortiert, `FromOpenFoodFacts = FoodFactsBarcode != null`, `Version = p.Version`), `StorageLocationDto ToDto(StorageLocation l)` – auch von E2 benutzt | – | 60 |
| `src/FoodAssistant.Infrastructure/Composition/CatalogInfrastructureRegistration.cs` | ändern (Stub) | `AddScoped<ICatalogStore, CatalogStore>()` | – | +1 |
| `src/FoodAssistant.Server/Catalog/CatalogEndpoints.cs` | ändern (Stub) | Gruppe `""` mit Tag `Catalog`: `GET /catalog/categories`; `GET /products?query&cursor&limit`; `GET /products/{productId:guid}`; `GET /products/by-barcode/{code}`; `POST /products` (201, Location `products/{id}`); `PUT /products/{productId:guid}` (If-Match Pflicht); `DELETE /products/{productId:guid}` (If-Match Pflicht, 204); `GET /storage-locations`; `POST /storage-locations`; `PUT/DELETE /storage-locations/{locationId:guid}` (If-Match Pflicht). If-Match über `IfMatch.TryRead`, Ergebnis über `ToHttpResult(http, dto => dto.Version)` | – | 170 |
| `tests/FoodAssistant.Server.Tests/Catalog/ProductEndpointTests.cs` | neu | Anlegen/Ändern/Archivieren, Validierung (Feldfehler), Barcode-Konflikt, ETag/412/428, Suche + Cursor, Barcode-Suche, Eingeschränkt → 403, **fremder Haushalt → 404**, Mindestbestand ruft Handler (Fake-`IStockChangedHandler` per `ConfigureTestServices`) | ja | 260 |
| `tests/FoodAssistant.Server.Tests/Catalog/StorageLocationEndpointTests.cs` | neu | CRUD, Typwechsel gesperrt mit Charge, Löschen gesperrt mit Charge, fremder Haushalt → 404, Eingeschränkt → 403 | ja | 180 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json` | ändern | neu erzeugen (`UPDATE_OPENAPI_SNAPSHOT=1`) | Snapshot-Test | gen. |
| `docs/kb/telemetrie.md` | ändern | Logs, Abläufe | – | +12 |

**Telemetrie:** Abläufe `foodassistant.catalog.create_product`, `.update_product`, `.archive_product`,
`.create_location`, `.update_location`, `.delete_location` (Attribut `foodassistant.outcome`);
Messwert `foodassistant.catalog.products.created` (Counter `{product}`, Attribut `source`: `manual`|`food_facts`).
**Akzeptanz:** Build, alle Tests grün, Snapshot aktualisiert, `check-file-length.ps1` grün.

### Paket A2 – Client und UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** A1 · **Blockiert durch:** –
- **Muster:** Seiten `UI/Shell/MorePage.razor`, Liste `UI/Common/ListRow.razor`, `EmptyState.razor`;
  Dialoge MudBlazor `MudDialog` + `IDialogService`; Fehler `UI/Common/ApiErrorAlert.razor` (S0.5);
  Client wie `Client/Accounts/AccountClient.cs` (S0.4); Tests `tests/FoodAssistant.UI.Tests/Shell/MorePageTests.cs` mit `FakeCatalogClient` (S0.5).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Catalog/CatalogClient.cs` | neu | implementiert `ICatalogClient` über `ApiHttpClient` und `ApiPaths.Household` | `CatalogClientTests` (Client.Tests): Pfade, If-Match, Query-Escaping | 130 |
| `src/FoodAssistant.Client/Composition/CatalogClientRegistration.cs` | ändern (Stub) | `TryAddScoped<ICatalogClient, CatalogClient>()` | – | +1 |
| `src/FoodAssistant.UI/Catalog/CatalogText.cs` + `.resx` + `.en.resx` | neu | alle Texte A2 inkl. `Error.catalog.*` | `ResourceCompletenessTests` | 5+2×70 |
| `src/FoodAssistant.UI/Catalog/ProductsPage.razor` | neu | `@page "/products"`: Suchfeld (Entprellung 300 ms), Liste (`ListRow`: `CategoryIcons.For`, Name, Nebenzeile Marke · Kategorie), „Mehr laden“ per `nextCursor`, Knopf „Produkt anlegen“, Klick öffnet `ProductEditDialog`; leer → `EmptyState`; Live: bei `Changed` mit `product` neu laden | `ProductsPageTests` | 140 |
| `src/FoodAssistant.UI/Catalog/ProductEditDialog.razor` | neu | Parameter `ProductDto? Product`, `CreateProductRequest? Prefill` (für C2/F), `RenderFragment? Notice` (oben im Dialog, z. B. OFF-Attribution von C2); Felder Name, Marke, `CategorySelect`, Standardeinheit, Standard-Lagerort, Bereich (optional, „automatisch nach Kategorie“), Mindestbestand (`QuantityInput`), Barcodes (Chips, hinzufügen/entfernen); Speichern mit ETag, 412 → Hinweis + neu laden; Archivieren (nur bestehend, Bestätigung); Link „Haltbarkeit anpassen“ → `/products/{id}/shelf-life` (Seite von B1); Rückgabe `DialogResult.Ok(ProductDto)` | `ProductEditDialogTests` (Pflichtfelder, Feldfehler am Feld, 412) | 200 |
| `src/FoodAssistant.UI/Catalog/CategorySelect.razor` | neu | `MudSelect<string>` über `ListCategoriesAsync`, Namen aus `CategoryText`, Symbol | – | 50 |
| `src/FoodAssistant.UI/Catalog/ProductPicker.razor` | neu | **Gemeinsame Komponente für B2, C2, D, F**: `MudAutocomplete<ProductSummaryDto>` über `SearchProductsAsync` (min. 1 Zeichen, 300 ms); letzte Option „„{Text}“ als neues Produkt anlegen“ öffnet `ProductEditDialog` mit Prefill; `[Parameter] EventCallback<ProductSummaryDto> ProductSelected`; `[Parameter] bool AllowCreate = true`; zugänglicher Name über `Label` | `ProductPickerTests` | 110 |
| `src/FoodAssistant.UI/Catalog/StorageLocationsPage.razor` | neu | `@page "/settings/locations"`: Liste mit Typ (Text + Symbol, nicht nur Farbe), Anlegen/Bearbeiten (`StorageLocationDialog`), Löschen mit Bestätigung, Fehler `catalog.location_in_use` verständlich; leer → Knopf „Standard-Lagerorte anlegen“ legt *Kühlschrank* (Chilled), *Gefrierschrank* (Frozen), *Vorratsschrank* (Ambient) mit lokalisierten Namen an | `StorageLocationsPageTests` | 150 |
| `src/FoodAssistant.UI/Catalog/StorageLocationDialog.razor` | neu | Name, Typ (Radio mit `OptionGroup` aus `UI/Common`), Hinweis bei gesperrtem Typwechsel | – | 80 |
| `tests/FoodAssistant.UI.Tests/Catalog/*.cs` | neu | oben genannte bUnit-Tests | ja | 4×120 |

**Barrierefreiheit:** Checkliste für Produktliste, Dialog, Picker (Tastatur in Autocomplete, Fokus
zurück nach Dialog), Lagerorte. **Akzeptanz:** Tests grün; Sichtprüfung im Browser-Pane:
Lagerorte anlegen, Produkt mit Barcode anlegen, suchen, bearbeiten, archivieren.
**Nicht Teil:** Haltbarkeits-Überschreibung (B1), Open-Food-Facts-Vorbelegung (C2).

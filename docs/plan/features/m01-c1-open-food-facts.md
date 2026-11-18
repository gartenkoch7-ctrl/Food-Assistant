# M1 C1 – Open Food Facts (Server, Cache)

Barcode → Produktvorschlag aus Open Food Facts, nur über den Server, mit serverweitem Cache
(auch „nicht gefunden“), eigenem `User-Agent` und Drosselung. Keine Bilder (F7, Annahme a),
keine Allergene/Nährwerte in M1. KB: [externe-daten](../../kb/externe-daten.md),
[datenschutz](../../kb/datenschutz.md) („nur der Barcode verlässt den Server“).

Branch `feat/m01-c1-food-facts` · ein Paket.

## Aufgaben
- [x] C1 OFF-Client, Cache, Kategorie-Zuordnung, Endpunkt, Tests, KB

## Detailplan

### Paket C1 – OFF-Anbindung

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Blockiert durch:** F8 (Merge erst mit Kontaktwert; Entwicklung mit Platzhalter), F7 (Annahme a)
- **Muster:** typisierter `HttpClient` über `AddHttpClient<T>` (Infrastructure); Optionen wie
  `Server/Api/RateLimiting/ApiRateLimitOptions.cs` (`BindConfiguration` + `ValidateOnStart`);
  Store wie `Infrastructure/Households/InvitationStore.cs`; Endpunkt wie `Server/Households/MemberEndpoints.cs`.

**API-Fakten (Open Food Facts, Stand 2026):**
- `GET https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=code,product_name,product_name_de,product_name_en,generic_name,brands,quantity,product_quantity,product_quantity_unit,categories_tags`
- Antwort: `{"status":1,"product":{…}}` gefunden, `{"status":0,…}` nicht gefunden (HTTP 404 oder 200 mit status 0 – beide als „nicht gefunden“ behandeln).
- Lesegrenze: 100 Produktabfragen pro Minute und IP → Server drosselt selbst auf **60/min**
  (`System.Threading.RateLimiting.TokenBucketRateLimiter`, Singleton); bei Ablehnung → „nicht verfügbar“.
- `User-Agent: FoodAssistant/<ServerVersion> (<Kontakt>)` – Version aus `Server/Api/Info/ServerVersion.cs`
  wird als Option durchgereicht; Kontakt aus F8 (`FoodAssistant:OpenFoodFacts:Contact`).
- Zeitlimit 5 s, keine Wiederholung im Request.

**Cache-Regeln:** gefunden → 30 Tage frisch; nicht gefunden → 2 Tage; abgelaufen → neu abfragen,
bei Fehler den abgelaufenen Eintrag liefern (`Stale = true`); `LastRequestedAt` bei jedem Treffer.
Kein Haushaltsbezug in der Tabelle (bewusst serverweit, KB). Namensauswahl: `product_name_<Sprache der Anfrage>`
→ `product_name` → `generic_name`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Application/FoodFacts/FoodFactsProduct.cs` | neu | `(string Barcode, string? Name, string? NameDe, string? NameEn, string? Brand, string? QuantityText, decimal? PackageQuantity, string? PackageUnit, IReadOnlyList<string> CategoryTags)` | – | 15 |
| `src/FoodAssistant.Application/FoodFacts/FoodFactsLookup.cs` + `FoodFactsStatus.cs` | neu | `FoodFactsLookup(FoodFactsStatus Status, FoodFactsProduct? Product, bool Stale)`; `enum FoodFactsStatus { Found, NotFound, Unavailable }` | – | 15+10 |
| `src/FoodAssistant.Application/FoodFacts/IFoodFactsApi.cs` | neu | `Task<FoodFactsLookup> FetchAsync(string barcode, CancellationToken ct)` (nie Exceptions für Netzfehler → `Unavailable`) | – | 15 |
| `src/FoodAssistant.Application/FoodFacts/IFoodFactsCache.cs` | neu | `Task<FoodFactsCacheHit?> GetAsync(string barcode, …)`; `Task StoreAsync(FoodFactsLookup lookup, DateTimeOffset now, …)`; `FoodFactsCacheHit(FoodFactsLookup Lookup, DateTimeOffset ExpiresAt)` | – | 20 |
| `src/FoodAssistant.Application/FoodFacts/FoodFactsService.cs` | neu | `LookupAsync(string barcode, string culture, CancellationToken)` → `FoodFactsLookup` nach Cache-Regeln; Activity `foodassistant.food_facts.lookup` mit `foodassistant.cache` = `hit`/`miss`/`stale`, `foodassistant.outcome`; Messwert | `FoodFactsServiceTests` (Fakes für Api/Cache, `MutableTimeProvider`-Muster): frisch, abgelaufen + Fehler → stale, nicht gefunden 2 Tage | 110 |
| `src/FoodAssistant.Application/FoodFacts/FoodFactsSuggestion.cs` | neu | `static FoodFactsProductDto ToDto(FoodFactsProduct p, string culture, Func<IReadOnlyList<string>, string?> categoryOf)`: Name nach Sprache, `PackageQuantity` nur bei Einheit g/kg/ml/l (OFF `product_quantity_unit`), sonst null | `FoodFactsSuggestionTests` | 50 |
| `src/FoodAssistant.Application/FoodFacts/FoodFactsLog.cs` | neu | `LookupFailed` (3350, Warning, `Reason` – z. B. `timeout`, `http_503`, `throttled`), `ServedStale` (3351, Information). Den **Barcode nie loggen** (erlaubt Rückschlüsse auf Einkäufe) | – | 25 |
| `src/FoodAssistant.Application/Composition/FoodFactsRegistration.cs` | ändern (Stub) | `AddScoped<FoodFactsService>()` | – | +1 |
| `src/FoodAssistant.Infrastructure/FoodFacts/OpenFoodFactsOptions.cs` | neu | Abschnitt `FoodAssistant:OpenFoodFacts`: `Enabled` (true), `BaseUrl` (`https://world.openfoodfacts.org/`), `Contact` (F8), `TimeoutSeconds` (5), `RequestsPerMinute` (60); Validierung: `BaseUrl` absolut https | `OpenFoodFactsOptionsTests` | 40 |
| `src/FoodAssistant.Infrastructure/FoodFacts/OpenFoodFactsApi.cs` | neu | typisierter Client: Drosselung, `User-Agent`, `fields`-Liste wie oben, JSON lesen (nur benötigte Felder, `JsonDocument`), Timeouts/HTTP-Fehler → `Unavailable`; `Enabled=false` → immer `Unavailable` ohne Aufruf | `OpenFoodFactsApiTests` mit Fake-Handler und **selbst erfundenen** JSON-Antworten („Testmilch 1 l“): gefunden, status 0, 404, 503, Timeout, Drosselung | 140 |
| `src/FoodAssistant.Infrastructure/FoodFacts/FoodFactsCategoryMap.cs` | neu | `internal static string? CategoryOf(IReadOnlyList<string> tags)`: erste Übereinstimmung aus fester Tabelle `en:…` → Kategorieschlüssel, spezifisch vor allgemein (z. B. `en:cheeses`→`cheese`, `en:yogurts`/`en:milks`/`en:dairies`→`dairy`, `en:eggs`→`eggs`, `en:poultries`→`poultry`, `en:meats`→`meat`, `en:sausages`/`en:hams`→`sausage`, `en:fishes`/`en:seafood`→`fish`, `en:fruits`→`fruit`, `en:vegetables`→`vegetables`, `en:herbs`→`herbs`, `en:breads`/`en:pastries`→`bread`, `en:pastas`/`en:rices`/`en:cereals-and-their-products`→`grains`, `en:legumes`→`legumes`, `en:canned-foods`→`canned`, `en:frozen-foods`→`frozen`, `en:meals`→`ready_meals`, `en:sauces`/`en:condiments`→`sauces`, `en:spices`→`spices`, `en:fats`/`en:vegetable-oils`→`oils`, `en:baking-decorations`/`en:flours`/`en:sugars`→`baking`, `en:snacks`/`en:sweet-snacks`/`en:chocolates`→`sweets`, `en:beverages`→`beverages`, `en:plant-based-foods-and-beverages` + `en:milk-substitutes`→`plant_based`); sonst null | `FoodFactsCategoryMapTests`: jedes Ziel ∈ `ProductCategories.All` | 80 |
| `src/FoodAssistant.Infrastructure/FoodFacts/FoodFactsCacheStore.cs` | neu | EF über `FoodFactsCacheEntry` (kein Haushaltsfilter – Entität nicht `IHouseholdOwned`); `PayloadJson` = serialisiertes `FoodFactsProduct`; Upsert | `FoodFactsCacheStoreTests` (Testcontainers) | 80 |
| `src/FoodAssistant.Infrastructure/FoodFacts/FoodFactsCategoryResolver.cs` | neu | `IFoodFactsCategoryResolver` (Application-Interface in `Application/FoodFacts/IFoodFactsCategoryResolver.cs`, 10 Zeilen) über `FoodFactsCategoryMap` | – | 20 |
| `src/FoodAssistant.Infrastructure/Composition/FoodFactsInfrastructureRegistration.cs` | ändern (Stub) | Optionen binden + `ValidateOnStart`, `AddHttpClient<IFoodFactsApi, OpenFoodFactsApi>`, Singleton-Drossel, `AddScoped<IFoodFactsCache, FoodFactsCacheStore>()`, Resolver | – | +15 |
| `src/FoodAssistant.Contracts/FoodFacts/FoodFactsProductDto.cs` + `FoodFactsErrorCodes.cs` | neu | `FoodFactsProductDto(string Barcode, string? Name, string? Brand, string? QuantityText, QuantityDto? PackageQuantity, string? SuggestedCategoryKey, bool Stale)`; Codes `food_facts.not_found` (404), `food_facts.unavailable` (503), `food_facts.invalid_barcode` (400) | – | 15+15 |
| `src/FoodAssistant.Server/FoodFacts/FoodFactsEndpoints.cs` | ändern (Stub) | `GET /food-facts/{barcode}` (`HouseholdAny`): Barcode prüfen (Ziffern, 8/12/13/14), Sprache aus `CultureInfo.CurrentUICulture` (Request-Lokalisierung), Ergebnis → 200/404/503 über `ApiProblems` | – | 50 |
| `tests/FoodAssistant.Server.Tests/FoodFacts/FoodFactsEndpointTests.cs` | neu | mit Fake-`IFoodFactsApi` (`ConfigureTestServices`): gefunden, nicht gefunden, nicht verfügbar, ungültiger Barcode, ohne Anmeldung 401, fremder Haushalt 404, Eingeschränkt erlaubt | ja | 140 |
| `tests/FoodAssistant.Infrastructure.Tests/FoodFacts/OpenFoodFactsLiveTests.cs` | neu | ein echter Aufruf, **standardmäßig übersprungen** (`[Fact(Explicit = true)]`), nur mit `FOODASSISTANT_LIVE_TESTS=1` | – | 30 |
| `src/FoodAssistant.Server/appsettings.json` | ändern | Abschnitt `FoodAssistant:OpenFoodFacts` mit Standardwerten (Kontakt laut F8) | – | +8 |
| `docs/kb/externe-daten.md` | ändern | Endpunkt, Felder, Cache-Fristen, Drosselung, User-Agent, „keine Bilder in M1“ | – | +15 |
| `docs/kb/datenschutz.md` | ändern | Zeile „Open-Food-Facts-Cache (Barcode + Produktdaten, ohne Haushalts- oder Personenbezug) · PostgreSQL · bis Ablauf + erneut“ | – | +1 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md` | ändern | Snapshot; Logs, Ablauf, Messwert `foodassistant.food_facts.lookups` (Counter `{lookup}`, `cache`, `outcome`) | – | – |

**Akzeptanz:** Tests grün (ohne Netz); Live-Test manuell einmal grün; KB aktualisiert; F8-Wert gesetzt.
**Nicht Teil:** Scan-UI und Produktanlage aus dem Vorschlag (C2), Bilder (F7).

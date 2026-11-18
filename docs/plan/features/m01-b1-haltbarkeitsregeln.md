# M1 B1 – Haltbarkeitsregeln (Standard + Überschreibungen)

Mitgelieferte Regeltabelle pro Kategorie und Lagerort-Typ (mit Quelle), Überschreibungen pro
Kategorie und Produkt, Auflösung Produkt → Kategorie → Standard (`ShelfLifeRuleResolver`, M0).
**Sicherheitsgrenze:** Die KI schlägt nie Haltbarkeiten vor; fehlt eine Regel, gibt es kein
berechnetes Datum. KB: [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md), ADR-0010, AGENTS.md §2.3.

Branch `feat/m01-b1-shelf-life` · Pakete: B1.2 → B1.3; B1.1 unabhängig (blockiert durch F2).

**Welche Zeile gilt wann** (verbindlich für B2/F):
- Öffnen: Zeile (Kategorie, aktueller Lagertyp der Charge) → `DaysAfterOpening`.
- Einfrieren: Zeile (Kategorie, `Frozen`) → `DaysFrozen`.
- Auftauen: Zeile (Kategorie, Ziel-Lagertyp) → `DaysAfterThawing`.
- Rest anlegen: Zeile (Kategorie, Lagertyp des Ziel-Lagerorts) → `DaysAsLeftover`.

## Aufgaben
- [ ] B1.1 Standardregel-Tabelle mit Quellen (Daten) – Freigabe durch den Nutzer
- [ ] B1.2 Regelquelle, Überschreibungen, `IShelfLifeRules`, Endpunkte
- [ ] B1.3 UI: Regelseite, Produkt-Überschreibung

## Detailplan

### Paket B1.1 – Standardregel-Tabelle

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** B1.2 (Dateiformat) · **Blockiert durch:** F2 (Merge der Werte erst nach Freigabe)
- Bis zur Freigabe bleibt die eingebettete Tabelle **leer** (`"rules": []`) – das ist sicher
  (keine berechneten Daten) und B1.2/B1.3 können gemergt werden.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Infrastructure/ShelfLife/default-shelf-life-rules.json` | ändern | Werte für alle 24 Kategorien und sinnvollen Lagertypen; je Zeile `source`-Schlüssel; eher vorsichtig (kürzere Frist bei Spannbreiten); nur Fakten (Tage), keine übernommenen Texte (Urheberrecht) | `DefaultShelfLifeRulesTests` (B1.2) | ~150 |
| `docs/kb/haltbarkeitsregeln.md` | neu | Tabelle lesbar (Kategorie × Lagertyp × 4 Werte), Quellenliste mit Titel/URL/Abrufdatum, Begründung bei Abweichungen, Datum der Freigabe durch den Nutzer; in `docs/kb/README.md` verlinken | – | 120 |

**Akzeptanz:** Nutzer hat die Tabelle in `haltbarkeitsregeln.md` freigegeben (Datum eingetragen); Tests grün.

### Paket B1.2 – Regelquelle, Überschreibungen, Endpunkte

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Schritt 0 · **Blockiert durch:** –
- **Muster:** wie A1 (`docs/plan/features/m01-a-katalog.md`); eingebettete Ressource wie übliches
  `<EmbeddedResource Include="ShelfLife/default-shelf-life-rules.json" />` im Infrastructure-csproj.

JSON-Format (Version 1):
```json
{ "version": 1,
  "sources": { "bzfe": { "title": "…", "url": "https://…", "retrieved": "2026-11-20" } },
  "rules": [ { "category": "dairy", "storageType": "Chilled", "daysAfterOpening": 3,
               "daysFrozen": null, "daysAfterThawing": 1, "daysAsLeftover": 2, "source": "bzfe" } ] }
```

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Infrastructure/ShelfLife/default-shelf-life-rules.json` | neu | Format oben, `"rules": []` | – | 10 |
| `src/FoodAssistant.Infrastructure/FoodAssistant.Infrastructure.csproj` | ändern | `EmbeddedResource` | – | +3 |
| `src/FoodAssistant.Application/ShelfLife/IDefaultShelfLifeRules.cs` | neu | `ShelfLifeRule? Find(string categoryKey, StorageType storageType)`; `IReadOnlyList<DefaultShelfLifeRow> Rows`; `IReadOnlyDictionary<string, ShelfLifeSource> Sources` | – | 20 |
| `src/FoodAssistant.Application/ShelfLife/DefaultShelfLifeRow.cs`, `ShelfLifeSource.cs` | neu | `DefaultShelfLifeRow(string CategoryKey, StorageType StorageType, ShelfLifeRule Rule, string SourceKey)`; `ShelfLifeSource(string Key, string Title, Uri Url, DateOnly Retrieved)` | – | 2×10 |
| `src/FoodAssistant.Infrastructure/ShelfLife/EmbeddedDefaultShelfLifeRules.cs` | neu | Singleton; lädt JSON einmal (`System.Text.Json`, strikt: unbekannte Kategorie, doppelte Zeile, negative Tage, fehlende Quelle → `InvalidOperationException` beim Start) | `DefaultShelfLifeRulesTests` (Infrastructure.Tests): echte Datei gültig; Fehlerfälle über Test-JSON-Strings (`internal static Parse(string json)`) | 110 |
| `src/FoodAssistant.Application/ShelfLife/IShelfLifeOverrideStore.cs` | neu | `GetForProductAsync(Guid productId, StorageType, …)` → `ShelfLifeRule?`; `GetForCategoryAsync(string category, StorageType, …)`; `ListCategoryOverridesAsync`, `ListProductOverridesAsync(Guid productId, …)`; `UpsertCategoryAsync(string, StorageType, ShelfLifeRule, …)`; `UpsertProductAsync(Guid, StorageType, ShelfLifeRule, …)` (Produkt nicht gefunden → `NotFound`); `DeleteCategoryAsync`, `DeleteProductAsync`; `GetCategoryOfProductAsync(Guid productId, …)` → `string?` | – | 35 |
| `src/FoodAssistant.Application/ShelfLife/ShelfLifeRuleService.cs` | neu | implementiert `IShelfLifeRules` (S0.2): `ResolveAsync(productId, storageType)` = `ShelfLifeRuleResolver.Resolve(productOverride, categoryOverride, default)`; `ResolveForCategoryAsync` ohne Produktebene | `ShelfLifeRuleServiceTests` (Application.Tests mit Fake-Store): Vorrang Produkt > Kategorie > Standard je Feld, ohne Regel → `Empty` | 70 |
| `src/FoodAssistant.Application/ShelfLife/ShelfLifeAdminService.cs` | neu | `ListRulesAsync(Guid householdId, …)` → `IReadOnlyList<ShelfLifeRuleRowDto>` (alle 24 Kategorien × 3 Lagertypen, auch ohne Standard), `ListSourcesAsync`, `SetCategoryOverrideAsync(Guid householdId, string category, StorageType, ShelfLifeDaysDto, …)`, `ResetCategoryOverrideAsync`, `GetProductOverridesAsync`, `SetProductOverrideAsync`, `ResetProductOverrideAsync`; Validierung: Tage 0–3650 oder null, Kategorie bekannt | über Endpunkt-Tests | 150 |
| `src/FoodAssistant.Application/ShelfLife/ShelfLifeLog.cs` | neu | `OverrideChanged` (3250, Information, `HouseholdId`, `Scope` = `category`/`product`, `StorageType`), `OverrideReset` (3251) | – | 25 |
| `src/FoodAssistant.Contracts/ShelfLife/ShelfLifeDaysDto.cs`, `ShelfLifeRuleRowDto.cs`, `ShelfLifeSourceDto.cs`, `ProductShelfLifeDto.cs`, `ShelfLifeErrorCodes.cs` | neu | `ShelfLifeDaysDto(int? DaysAfterOpening, int? DaysFrozen, int? DaysAfterThawing, int? DaysAsLeftover)`; `ShelfLifeRuleRowDto(string CategoryKey, StorageTypeDto StorageType, ShelfLifeDaysDto Default, ShelfLifeDaysDto? Override, ShelfLifeDaysDto Effective, string? SourceKey)`; `ShelfLifeSourceDto(string Key, string Title, string Url, DateOnly Retrieved)`; `ProductShelfLifeDto(Guid ProductId, string CategoryKey, IReadOnlyList<ShelfLifeRuleRowDto> Rows)`; Codes `shelf_life.validation_failed`, `shelf_life.unknown_category`, `catalog.product_not_found` (wiederverwendet) | – | 5×12 |
| `src/FoodAssistant.Infrastructure/ShelfLife/ShelfLifeOverrideStore.cs` | neu | EF über `db.Set<ShelfLifeOverride>()`; Upsert per vorhandener Zeile | `ShelfLifeOverrideStoreTests` (Testcontainers) | 140 |
| `src/FoodAssistant.Application/Composition/ShelfLifeRegistration.cs` | ändern (Stub) | **`AddScoped<IShelfLifeRules, ShelfLifeRuleService>()`** (kein `TryAdd`, ersetzt den Platzhalter aus S0.2), `AddScoped<ShelfLifeAdminService>()` | `ShelfLifeRegistrationTests`: aufgelöst wird `ShelfLifeRuleService` | +3 |
| `src/FoodAssistant.Infrastructure/Composition/ShelfLifeInfrastructureRegistration.cs` | ändern (Stub) | `AddSingleton<IDefaultShelfLifeRules, EmbeddedDefaultShelfLifeRules>()`, `AddScoped<IShelfLifeOverrideStore, ShelfLifeOverrideStore>()` | – | +2 |
| `src/FoodAssistant.Server/ShelfLife/ShelfLifeEndpoints.cs` | ändern (Stub) | `GET /shelf-life/rules` (Any), `GET /shelf-life/sources` (Any), `PUT /shelf-life/overrides/categories/{categoryKey}/{storageType}` (Member, Body `ShelfLifeDaysDto`), `DELETE` dito (Member, 204); `GET /products/{productId:guid}/shelf-life` (Any) → `ProductShelfLifeDto`; `PUT/DELETE /products/{productId:guid}/shelf-life/{storageType}` (Member). Überschreibungen ohne If-Match (Upsert, letzte gewinnt; begründet im Kommentar) | – | 110 |
| `tests/FoodAssistant.Server.Tests/ShelfLife/ShelfLifeEndpointTests.cs` | neu | Liste mit Standard/Eigen/Effektiv, Kategorie- und Produktüberschreibung, Reset, Validierung, Eingeschränkt → 403, fremder Haushalt/fremdes Produkt → 404 | ja | 220 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md` | ändern | Snapshot neu; Logs + Abläufe `foodassistant.shelf_life.set_override`, `.reset_override` | – | – |

**Akzeptanz:** Tests grün; mit leerer Tabelle liefert `ResolveAsync` `Empty`; Snapshot aktualisiert.

### Paket B1.3 – UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** B1.2 · **Muster:** wie A2.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/ShelfLife/IShelfLifeClient.cs` + `ShelfLifeClient.cs` | neu | Methoden zu allen B1.2-Endpunkten | `ShelfLifeClientTests` | 30+80 |
| `src/FoodAssistant.Client/Composition/ShelfLifeClientRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.UI/ShelfLife/ShelfLifeText.cs` + `.resx` + `.en.resx` | neu | Texte inkl. Hinweis „Werte aus geprüften Quellen; die KI schlägt keine Haltbarkeiten vor“, „Fehlt eine Regel, gibt es kein berechnetes Datum“ | – | 5+2×50 |
| `src/FoodAssistant.UI/ShelfLife/ShelfLifeRulesPage.razor` | neu | `@page "/settings/shelf-life"`: je Kategorie (Name + Symbol) eine aufklappbare Gruppe mit Zeilen pro Lagertyp; vier Werte, Kennzeichnung **„Standard“ / „Eigen“** als Text (nicht nur Farbe), Quellen-Fußnote; Bearbeiten (Member+) per `ShelfLifeDaysDialog`, „Auf Standard zurücksetzen“ | `ShelfLifeRulesPageTests` | 150 |
| `src/FoodAssistant.UI/ShelfLife/ShelfLifeDaysDialog.razor` | neu | vier optionale Zahlenfelder 0–3650 mit Einheit „Tage“, Feldfehler am Feld | `ShelfLifeDaysDialogTests` | 90 |
| `src/FoodAssistant.UI/ShelfLife/ProductShelfLifePage.razor` | neu | `@page "/products/{ProductId:guid}/shelf-life"`: gleiche Darstellung nur für ein Produkt (Ebene Produkt) | `ProductShelfLifePageTests` | 110 |
| `tests/FoodAssistant.UI.Tests/ShelfLife/*` + `Support/FakeShelfLifeClient.cs` | neu | bUnit | ja | 3×100+60 |

**Barrierefreiheit:** Checkliste (Tabellen-/Gruppenstruktur mit Überschriften, „Standard/Eigen“ als Text).
**Akzeptanz:** Tests grün; Sichtprüfung: Kategorie überschreiben, zurücksetzen.

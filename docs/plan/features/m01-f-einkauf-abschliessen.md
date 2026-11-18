# M1 F – Einkauf abschließen

Gekaufte (abgehakte) Einträge einer Liste gesammelt als Chargen in den Vorrat buchen: Vorschlag
mit vorbelegter Menge, Lagerort und Ablaufdatum, schnell korrigierbar; Freitext einem Produkt
zuordnen (auch neu anlegen) oder überspringen; danach verschwinden die Einträge von der Liste.
KB: [einkauf](../../kb/einkauf.md) „Abhaken und Einkauf abschließen“, ADR-0010.

Branch `feat/m01-f-checkout` · Welle 2 · Pakete: F1 → F2.

## Aufgaben
- [ ] F1 Vorbelegung (Domain), Vorschlag und Abschluss (Server, eine Transaktion)
- [ ] F2 Abschluss-Seite (UI)

## Fachregeln (verbindlich)

- Grundlage: alle Einträge der Liste mit `Checked = true`, nicht gelöscht.
- **Menge:** Menge des Eintrags, sonst `1 × Standardeinheit des Produkts`, bei Freitext `1 piece`.
- **Lagerort:** `Product.DefaultStorageLocationId` → Ort der zuletzt angelegten Charge des Produkts
  → erster Ort (nach `SortOrder`) mit `ProductCategories.DefaultStorageType(category)` → erster Ort.
  Kein Lagerort im Haushalt → Vorschlag ohne Ort; Abschluss verlangt einen (400 Feldfehler).
- **Ablaufdatum** (`CheckoutPrefill.Propose`, reine Domain-Logik):
  - Ziel gefroren **und** kein „letzter Kauf“ → Regel `DaysFrozen` ab heute (Art `UseBy`, „aus Regel“) bzw. ohne Regel kein Datum (F5: wie selbst eingefroren).
  - „Letzter Kauf“ bekannt (`Product.LastShelfLifeDays`, `LastExpiryKind`) → heute + Tage, Art wie zuletzt.
  - sonst kein Datum (`ExpiryKind.None`) – Nutzer trägt ein.
- **Abschluss** (`POST`, Idempotency-Key vom Client): je Zeile entweder `Skip` oder Charge anlegen
  (Herkunft `Purchase`, Regeln wie B2 „Neue Charge“, inkl. F5-Zweig) – neues Produkt inline
  möglich (Name, Kategorie, Standardeinheit; Barcode nicht). Nach dem Anlegen
  `Product.LastShelfLifeDays = ExpiresOn − heute` (nur wenn Datum und Packungsdatum vom Nutzer, nicht aus Regel)
  und `LastExpiryKind`. Alle verarbeiteten Einträge (auch übersprungene) bekommen `DeletedAt`,
  `Version + 1`. Einträge, die inzwischen nicht mehr abgehakt oder gelöscht sind oder deren
  `Version` ≠ `ItemVersion` ist, werden **nicht** verarbeitet und im Ergebnis gemeldet.
- **Eine Transaktion** für Produkte, Chargen, Einträge, Aktivitäten; danach `IStockChangedHandler`
  für alle betroffenen Produkte (Mindestbestand wird wieder „scharf“, G).
- Rechte: `HouseholdMember`.

## Detailplan

### Paket F1 – Server

- **Stufe / Modell / Effort:** 3 / Opus / high
- **Wartet auf:** Merge B2, D · **Blockiert durch:** F5 (nur Gefrier-Zweig, wie B2)
- **Muster:** Store-Muster `docs/kb/datenmodell.md`; `StockBatchMapping.NewRecord` (S0.1);
  `BatchDtoMapper` (B2); Domain wie `src/FoodAssistant.Domain/Inventory/MinimumStockPolicy.cs`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Checkout/CheckoutPrefill.cs` + `ExpiryProposal.cs` | neu | `public static ExpiryProposal Propose(StorageType targetType, int? lastShelfLifeDays, ExpiryKind? lastExpiryKind, ShelfLifeRule frozenRule, DateOnly today)`; `ExpiryProposal(DateOnly? ExpiresOn, ExpiryKind Kind, bool FromRule)` | `CheckoutPrefillTests` (alle Zweige) | 50+10 |
| `src/FoodAssistant.Contracts/Checkout/CheckoutProposalDto.cs`, `CheckoutEntryDto.cs`, `CompleteCheckoutRequest.cs`, `CheckoutLineRequest.cs`, `NewProductLine.cs`, `CheckoutResultDto.cs`, `CheckoutErrorCodes.cs` | neu | `CheckoutEntryDto(Guid ItemId, long ItemVersion, Guid? ProductId, string Text, string? CategoryKey, QuantityDto Quantity, Guid? StorageLocationId, DateOnly? ExpiresOn, ExpiryKindDto ExpiryKind, bool ExpiryFromRule)`; `CheckoutProposalDto(Guid ListId, IReadOnlyList<CheckoutEntryDto> Entries)`; `CheckoutLineRequest(Guid ItemId, long ItemVersion, bool Skip, Guid? ProductId, NewProductLine? NewProduct, QuantityDto? Quantity, Guid? StorageLocationId, DateOnly? ExpiresOn, ExpiryKindDto ExpiryKind, bool SelfFrozen)`; `NewProductLine(string Name, string CategoryKey, string DefaultUnit)`; `CompleteCheckoutRequest(IReadOnlyList<CheckoutLineRequest> Lines)`; `CheckoutResultDto(int BatchesCreated, int ItemsRemoved, IReadOnlyList<Guid> BatchIds, IReadOnlyList<Guid> StaleItemIds)`; Codes `checkout.list_not_found`, `checkout.validation_failed`, `checkout.nothing_to_complete` | – | 7×12 |
| `src/FoodAssistant.Application/Checkout/ICheckoutStore.cs` | neu | `LoadAsync(Guid listId, …)` → `CheckoutData?` (Einträge abgehakt, zugehörige Produkte inkl. `LastShelfLifeDays`, Lagerorte, letzte Charge je Produkt); `CommitAsync(CheckoutCommit commit, …)` → `OperationResult<CheckoutResultDto>` | – | 25 |
| `src/FoodAssistant.Application/Checkout/{CheckoutData,CheckoutCommit}.cs` | neu | Records (Domain-Chargen, neue Produkte, zu löschende Einträge mit erwarteter Version, Produkt-Aktualisierungen) | – | 2×30 |
| `src/FoodAssistant.Application/Checkout/CheckoutService.cs` | neu | `ProposeAsync(Guid householdId, Guid listId, …)` → `OperationResult<CheckoutProposalDto>`; `CompleteAsync(Guid householdId, Guid listId, CompleteCheckoutRequest, Guid userId, …)`: validieren (Menge, Ort gehört zum Haushalt, Datum ↔ Art), Regeln über `IShelfLifeRules.ResolveAsync`/`ResolveForCategoryAsync`, Domain-Chargen bauen, Commit, Aktivitäten (`batch_added`), Hook | über Endpunkt-Tests | 220 |
| `src/FoodAssistant.Application/Checkout/CheckoutLog.cs` | neu | `CheckoutCompleted` (3500, Information, `ListId`, `BatchCount`, `SkippedCount`, `StaleCount`) | – | 20 |
| `src/FoodAssistant.Application/Composition/CheckoutRegistration.cs` | ändern (Stub) | `AddScoped<CheckoutService>()` | – | +1 |
| `src/FoodAssistant.Infrastructure/Checkout/CheckoutStore.cs` | neu | liest/schreibt `ShoppingItem`, `Product`, `StockBatchRecord` in **einem** `SaveChangesAsync`; Versionsprüfung Einträge über `long Version` (Concurrency-Token) – Konflikt → betroffene Einträge als `StaleItemIds` und Commit ohne sie wiederholen (einmal) | `CheckoutStoreTests` (Testcontainers): alles oder nichts bei Fehler | 220 |
| `src/FoodAssistant.Infrastructure/Composition/CheckoutInfrastructureRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.Server/Checkout/CheckoutEndpoints.cs` | ändern (Stub) | `GET /shopping/lists/{listId:guid}/checkout` (Member), `POST /shopping/lists/{listId:guid}/checkout` (Member) | – | 50 |
| `tests/FoodAssistant.Server.Tests/Checkout/CheckoutEndpointTests.cs` | neu | Vorschlag (Ort-Kaskade, Datum aus letztem Kauf, Gefrier-Regel), Abschluss legt Chargen an und entfernt Einträge, Überspringen, neues Produkt inline, veralteter Eintrag → `StaleItemIds`, Wiederholung mit gleichem Idempotency-Key → gleiche Antwort, keine Doppelbuchung, Eingeschränkt 403, fremder Haushalt 404, Hook aufgerufen, Live-Änderungen (ChangeLog-Zeilen für Chargen und Einträge) | ja | 280 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md`, `docs/kb/einkauf.md` | ändern | Snapshot; Ablauf `foodassistant.checkout.complete` (`foodassistant.batches`, `foodassistant.outcome`), Messwert `foodassistant.checkout.batches` (Counter `{batch}`); KB: Vorbelegungs-Kaskade dokumentieren | – | – |

### Paket F2 – UI

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** F1 · **Muster:** B2.3-Dialoge, `ProductPicker` (A2), `DateOnlyField`, `QuantityInput` (S0.5).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Checkout/ICheckoutClient.cs` + `CheckoutClient.cs` | neu | `GetProposalAsync`, `CompleteAsync(…, Guid idempotencyKey, …)` (Schlüssel einmal pro Seitenaufruf erzeugen, bei Wiederholung gleich) | `CheckoutClientTests` | 15+50 |
| `src/FoodAssistant.Client/Composition/CheckoutClientRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.UI/Checkout/CheckoutPage.razor` | neu | `@page "/shopping/{ListId:guid}/complete"` (Member+, sonst Hinweis): kompakte Zeile je Eintrag (Name, Menge, Lagerort-Auswahl, Datum-Chip mit Art und Kennzeichen „aus Regel“ als Text); Tippen öffnet `CheckoutLineEditor`; Freitext-Zeilen: „Produkt zuordnen“ (`ProductPicker`, inkl. neu) oder „Überspringen“; Knopf „In den Vorrat buchen (n)“; Ergebnis: Ansage + Snackbar, `StaleItemIds` als Hinweis; zurück zur Liste | `CheckoutPageTests` | 220 |
| `src/FoodAssistant.UI/Checkout/CheckoutLineEditor.razor` | neu | Menge, Ort, Datum + Art (MHD/Verbrauchsdatum/keins), bei Gefrierort „selbst eingefroren“ | `CheckoutLineEditorTests` | 120 |
| `src/FoodAssistant.UI/Checkout/CheckoutText.cs` + `.resx` + `.en.resx` | neu | Texte, `Error.checkout.*` | – | 5+2×40 |
| `tests/FoodAssistant.UI.Tests/Checkout/*` + `Support/FakeCheckoutClient.cs` | neu | bUnit | ja | 2×140+60 |

**Barrierefreiheit:** Checkliste; jede Zeile mit Überschrift/Name; „aus Regel“ als Text.
**Akzeptanz:** Tests grün; Sichtprüfung: Liste abhaken → abschließen → Chargen im Vorrat, Liste leer.

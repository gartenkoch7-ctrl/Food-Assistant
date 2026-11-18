# M1 G – Mindestbestand und wiederkehrende Artikel

Fällt der Bestand eines Produkts unter seinen Mindestbestand, kommt es **einmal** automatisch auf
die Standardliste (markiert „automatisch“); erneut erst nach Wiederauffüllen. Wiederkehrende
Artikel (täglich, wöchentlich, monatlich) erscheinen zum Termin in der Zeitzone des Haushalts,
nicht doppelt, solange der letzte noch offen ist. KB: [einkauf](../../kb/einkauf.md).

Branch `feat/m01-g-restock` · Welle 2 · Pakete: G1 → G2.

## Aufgaben
- [x] G1 Mindestbestand (Handler auf `IStockChangedHandler`)
- [x] G2 Wiederkehrende Artikel (Regeln, Hintergrunddienst, UI)

## Detailplan

### Paket G1 – Mindestbestand

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Merge A, B2, D · **Blockiert durch:** –
- **Muster:** `src/FoodAssistant.Domain/Inventory/MinimumStockPolicy.cs`; Aufrufer rufen den Hook
  bereits (A bei Mindestbestand-Änderung, B2 bei Verbrauch/Einbuchen, F bei Abschluss).

**Regel `RestockDecision.Decide(Quantity? minimum, Quantity total, bool triggered, bool onOpenList)`:**
- `minimum` null → `Disarm` wenn `triggered`, sonst `None`.
- `total < minimum`: `!triggered && !onOpenList` → `AddToList(amount)`; `!triggered && onOpenList` → `Arm` (nur Merker setzen, nichts doppelt); sonst `None`.
- `total ≥ minimum` und `triggered` → `Disarm`.
- `amount` = `minimum − total` in Einheit des Mindestbestands, aufgerundet auf ganze Zahl bei
  `piece`/`package`/`can`, sonst exakt; nicht umrechenbar → `minimum`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Inventory/RestockDecision.cs` + `RestockAction.cs` | neu | Regel oben; `RestockAction(RestockKind Kind, Quantity? Amount)` mit `enum RestockKind { None, AddToList, Arm, Disarm }` | `RestockDecisionTests` (alle Fälle, Einheiten) | 60+20 |
| `src/FoodAssistant.Application/Restock/IRestockStore.cs` | neu | `GetProductStockAsync(Guid productId, …)` → `(string Name, Quantity? Minimum, bool Triggered, IReadOnlyList<StockBatch> Batches, bool OnOpenList)?`; `SetTriggeredAsync(Guid productId, bool triggered, …)` | – | 20 |
| `src/FoodAssistant.Application/Restock/MinimumStockHandler.cs` | neu | implementiert `IStockChangedHandler`; je Produkt `RestockDecision.Decide`; `AddToList` → `ShoppingListService.EnsureDefaultListAsync` + `ShoppingItemService.AddInternalAsync(… Origin = MinimumStock, ProductId, Quantity = Amount)` + Triggered = true; `Arm`/`Disarm` → Merker | `MinimumStockHandlerTests` (Server.Tests über API: Verbrauch unter Minimum → Eintrag; zweiter Verbrauch → kein zweiter; Eintrag löschen → kommt nicht wieder; Einkauf abschließen über Minimum → wieder scharf; nächstes Unterschreiten → neuer Eintrag; schon manuell auf Liste → kein Eintrag) | 90 |
| `src/FoodAssistant.Application/Restock/RestockLog.cs` | neu | `RestockItemAdded` (3550, Information, `ProductId`, `ListId`), `RecurringItemAdded` (3551, `RuleId`, `ListId`), `RecurringItemSkipped` (3552, `RuleId`), `RecurringRunFailed` (3553, Warning, `HouseholdId`, Exception) | – | 30 |
| `src/FoodAssistant.Application/Restock/RestockMetrics.cs` | neu | `foodassistant.restock.items_added` (Counter `{item}`, `origin`: `minimum_stock`/`recurring`) | – | 20 |
| `src/FoodAssistant.Infrastructure/Restock/RestockStore.cs` | neu | EF: Produkt + Chargen (`StockBatchMapping.ToDomain`) + `ShoppingItems.Any(ProductId, !Checked)` | – | 90 |
| `src/FoodAssistant.Application/Composition/RestockRegistration.cs`, `Infrastructure/Composition/RestockInfrastructureRegistration.cs` | ändern (Stub) | `AddScoped<IStockChangedHandler, MinimumStockHandler>()`; Store | – | +3 |
| `docs/kb/einkauf.md`, `docs/kb/telemetrie.md` | ändern | Regel „Arm/Disarm“, Mengenberechnung; Logs/Messwert | – | +10 |

### Paket G2 – Wiederkehrende Artikel

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** G1 · **Muster:** Hintergrunddienst mit `PeriodicTimer(TimeSpan, TimeProvider)`
  (wie übliches `BackgroundService`), pro Haushalt `IHouseholdScopeFactory` (S0.2); Endpunkte wie A1.

**Termine `RecurrenceSchedule.NextDue(RecurrenceFrequency f, DayOfWeek? dow, int? dom, DateOnly after)`:**
täglich → `after + 1`; wöchentlich → nächster `dow` **nach** `after`; monatlich → Tag `dom` des
nächsten Monats nach `after`, bei kürzeren Monaten letzter Tag (31 → 30./28./29.).
Erster Termin beim Anlegen: `NextDue(…, gestern)` (heute zählt, wenn passend).

**Lauf (alle 15 min):** je Haushalt (`ListActiveHouseholdIdsAsync`) im Scope: heute laut
`IHouseholdCalendar`; Regeln mit `NextDueOn ≤ heute`: letzter erzeugter Eintrag noch offen (nicht
abgehakt, nicht gelöscht) → überspringen; sonst Eintrag (`Origin = Recurring`) in Ziel-Liste, `LastCreatedItemId` setzen;
in beiden Fällen `NextDueOn = NextDue(…, heute)`. Fehler je Haushalt loggen, weiter mit dem nächsten.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Shopping/RecurrenceSchedule.cs` | neu | Regel oben + `static bool IsValid(f, dow, dom)` (wöchentlich braucht `dow`, monatlich `dom` 1–31) | `RecurrenceScheduleTests` (Monatsende, Schaltjahr, Wochenwechsel) | 60 |
| `src/FoodAssistant.Contracts/Restock/RecurringRuleDto.cs`, `RecurringRuleRequest.cs`, `RecurrenceFrequencyDto.cs`, `RestockErrorCodes.cs` | neu | `RecurringRuleDto(Guid Id, Guid ListId, Guid? ProductId, string Text, QuantityDto? Quantity, RecurrenceFrequencyDto Frequency, DayOfWeek? DayOfWeek, int? DayOfMonth, DateOnly NextDueOn, long Version)`; Request ohne Id/NextDue/Version (+ `Guid? Id`); Codes `restock.rule_not_found`, `restock.validation_failed` | – | 4×12 |
| `src/FoodAssistant.Application/Restock/IRecurringRuleStore.cs` + `RecurringRuleService.cs` | neu | CRUD (Member), Validierung (Liste im Haushalt, Text 1–200 oder Produkt) | über Endpunkt-Tests | 25+120 |
| `src/FoodAssistant.Application/Restock/RecurringItemsRunner.cs` | neu | `Task<int> RunAsync(CancellationToken)` für den **aktiven** Haushalt (Lauf oben) | `RecurringItemsRunnerTests` (Infrastructure.Tests mit `TestDatabase.CreateScope(activeHousehold)` und fester Zeit) | 90 |
| `src/FoodAssistant.Application/Restock/RecurringItemsJob.cs` | neu | `BackgroundService`: alle 15 min (`TimeProvider`), je Haushalt Scope über `IHouseholdScopeFactory`, `RecurringItemsRunner` auflösen | `RecurringItemsJobTests` (ein Durchlauf über zwei Haushalte; Fehler in einem stoppt den anderen nicht) | 70 |
| `src/FoodAssistant.Infrastructure/Restock/RecurringRuleStore.cs` | neu | EF über `RecurringShoppingRule` | – | 120 |
| `src/FoodAssistant.Application/Composition/RestockRegistration.cs`, `Infrastructure/Composition/RestockInfrastructureRegistration.cs` | ändern | Service, Runner, `AddHostedService<RecurringItemsJob>()`; Store | – | +4 |
| `src/FoodAssistant.Server/Restock/RestockEndpoints.cs` | ändern (Stub) | `GET/POST /shopping/recurring`, `PUT/DELETE /shopping/recurring/{ruleId:guid}` (Lesen Any, Schreiben Member, If-Match bei PUT/DELETE) | `RecurringRuleEndpointTests` (CRUD, Rechte, fremder Haushalt, Liste fremden Haushalts → 404) | 80+180 |
| `src/FoodAssistant.Client/Restock/IRestockClient.cs` + `RestockClient.cs`, `Client/Composition/RestockClientRegistration.cs` | neu/ändern | CRUD | `RestockClientTests` | 15+60+1 |
| `src/FoodAssistant.UI/Restock/RecurringItemsPage.razor` + `RecurringRuleDialog.razor` + `RestockText.cs` + resx | neu | `@page "/shopping/recurring"`: Liste („Milch · wöchentlich, Montag · Einkaufsliste · nächster Termin 24.11.“), Anlegen/Bearbeiten (Produkt über `ProductPicker` oder Text, Menge, Liste, Häufigkeit mit passenden Feldern), Löschen | `RecurringItemsPageTests`, `RecurringRuleDialogTests` | 130+130+5+2×30 |
| `tests/FoodAssistant.UI.Tests/Restock/*` + `Support/FakeRestockClient.cs` | neu | bUnit | ja | 2×110+60 |
| `tests/FoodAssistant.Server.Tests/Api/openapi-v1.json`, `docs/kb/telemetrie.md` | ändern | Snapshot; Abläufe `foodassistant.restock.recurring_run` (`foodassistant.items`), `.minimum_stock` | – | – |

**Barrierefreiheit:** Checkliste. **Akzeptanz:** Tests grün; Sichtprüfung: Mindestbestand am Produkt setzen,
verbrauchen → Eintrag „automatisch“ auf der Standardliste; wiederkehrende Regel „täglich“ → Eintrag nach Lauf.

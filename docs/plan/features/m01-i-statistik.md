# M1 I – Verschwendungsstatistik

Pro Monat verbraucht vs. entsorgt (Anzahl, Menge), Gründe, häufigste entsorgte Produkte, Trend –
haushaltsweit, nicht pro Person; Geldwert erst M5. Quelle: `ConsumptionEvents` ohne `UndoneAt`.
KB: [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md) „Statistik (MVP)“.

Branch `feat/m01-i-statistics` · M1b · ein Paket. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] I Aggregation (Domain), Endpunkt, Seite mit Diagramm und Tabelle

## Detailplan

### Paket I – Statistik

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** –
- **Muster:** Domain wie `src/FoodAssistant.Domain/Inventory/MinimumStockPolicy.cs`; Endpunkte wie A1; Seite wie A2.

**Regeln:** Zeitraum = letzte `months` Kalendermonate inkl. aktuellem (1–24, Standard 6) nach
`OccurredOn` (Haushaltsdatum). „Verbraucht“ = `UsedUp` + `Partial`, „entsorgt“ = `Discarded`.
Mengen je Dimension in Basiseinheit (g, ml, Stück); `None`-Einheiten (Packung, Dose …) als Anzahl
Ereignisse. Top 5 entsorgte Produkte nach Ereigniszahl (Gleichstand: Name). Trend =
Entsorgt-Anteil des aktuellen Monats minus Durchschnitt der drei Vormonate (Prozentpunkte; `null`
bei < 3 Vormonaten mit Daten).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Statistics/ConsumptionFact.cs` + `WasteStatistics.cs` + `WasteReport.cs` | neu | `ConsumptionFact(DateOnly OccurredOn, ConsumptionKind Kind, ConsumptionReason? Reason, Guid ProductId, Quantity Quantity)`; `static WasteReport Compute(IEnumerable<ConsumptionFact>, DateOnly today, int months, int top = 5)`; `WasteReport(IReadOnlyList<MonthBucket> Months, IReadOnlyDictionary<ConsumptionReason,int> Reasons, IReadOnlyList<(Guid ProductId, int Count)> TopDiscarded, decimal? TrendPoints)` | `WasteStatisticsTests` (Monatsgrenzen, Einheiten, Trend null/positiv/negativ, leer) | 15+120+30 |
| `src/FoodAssistant.Contracts/Statistics/WasteStatisticsDto.cs` (+ `MonthBucketDto`, `ReasonCountDto`, `TopProductDto`) | neu | DTOs; Produktname aus Katalog (auch archiviert) | – | 4×12 |
| `src/FoodAssistant.Application/Statistics/IStatisticsStore.cs` + `StatisticsService.cs` + `StatisticsLog.cs` | neu | Store lädt Fakten im Zeitraum (`UndoneAt == null`) und Produktnamen; Service rechnet über Domain; Log `StatisticsComputed` (3650, Debug, `Months`, `FactCount`) | über Endpunkt-Tests | 20+60+15 |
| `src/FoodAssistant.Infrastructure/Statistics/StatisticsStore.cs` | neu | EF-Projektion, Index `(HouseholdId, OccurredOn)` aus S0 | – | 60 |
| Registrierungen (Stubs Statistics) | ändern | | – | +2 |
| `src/FoodAssistant.Server/Statistics/StatisticsEndpoints.cs` | ändern (Stub) | `GET /statistics/waste?months=6` (Any) | `WasteStatisticsEndpointTests` (Daten über Verbrauchs-API erzeugen, Rückgängig zählt nicht, fremder Haushalt 404) | 30+180 |
| `src/FoodAssistant.Client/Statistics/*`, Registrierung | neu | `GetWasteAsync` | `StatisticsClientTests` | 50 |
| `src/FoodAssistant.UI/Statistics/StatisticsPage.razor` + `StatisticsText.cs` + resx | neu | `@page "/statistics"`: Monatswahl (3/6/12), `MudChart` Balken (verbraucht vs. entsorgt je Monat) **plus** gleichwertige Datentabelle (Diagramm `aria-hidden`, Tabelle für Screenreader sichtbar), Gründe als Liste mit Anzahl, Top-Produkte, Trend als Satz („2 Prozentpunkte weniger entsorgt als im Schnitt“); leer → `EmptyState` | `StatisticsPageTests` | 200+5+2×35 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Statistik“ | `MorePageTests` | +5 |
| `tests/FoodAssistant.UI.Tests/Statistics/*` + Fake | neu | bUnit | ja | 150 |
| `docs/kb/telemetrie.md`, `openapi-v1.json` | ändern | Log, Ablauf `foodassistant.statistics.waste` | – | – |

**Barrierefreiheit:** Checkliste; Diagramm nie einzige Informationsquelle; Farben aus Status-Tokens mit Text.
**Akzeptanz:** Tests grün; Sichtprüfung mit Testdaten.

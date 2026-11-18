# M1 V – Aktivitätsverlauf

Verlauf pro Haushalt für alle Mitglieder („Anna hat Milch geöffnet“), 90 Tage (ADR-0002),
inklusive Konfliktauflösungen der Einkaufsliste (ADR-0005) und „Rückgängig“ für Vorratsaktionen
(B2-Endpunkt). Daten schreiben B2 und D bereits in `HouseholdActivities` (S0); Aufbewahrung: E2.

Branch `feat/m01-v-activity` · M1b · ein Paket. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] V Endpunkt mit Cursor, Seite mit verständlichen Sätzen

## Detailplan

### Paket V – Verlauf

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Merge E2 (Aufbewahrung) · **Blockiert durch:** F4 (nur bei Antwort a/b relevant; bei c entfällt die Spur)
- **Muster:** Pagination wie A1-Produktsuche; Seite wie A2.

**Darstellung:** Person = Anzeigename des Mitglieds, sonst „ehemaliges Mitglied“ (Konto gelöscht,
`UserId` unbekannt) bzw. „automatisch“ (`UserId` null = System). Produktname aus Katalog (auch
archiviert). Satzvorlagen je `Area`+`Kind` in `ActivityText.resx` (`Inventory.batch_opened` =
„{0} hat {1} geöffnet“ usw. für alle `InventoryActivityKinds` und die Shopping-Arten aus D1, inkl.
Konflikt: „Abhaken von {0} hat eine gleichzeitige Änderung überschrieben“).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Contracts/Activity/ActivityEntryDto.cs` | neu | `(Guid Id, string Area, string Kind, Guid SubjectId, Guid? ProductId, string? ProductName, string? PersonName, ActivityPersonKind PersonKind, DateTimeOffset OccurredAt, bool Undone, bool CanUndo)`; `enum ActivityPersonKind { Member, FormerMember, System }` | – | 20 |
| `src/FoodAssistant.Application/Activity/IActivityFeedStore.cs` + `ActivityFeedService.cs` | neu | `ListAsync(ActivityCursor? after, int take, string? area)`; `CanUndo` = Area Inventory, ≤ 24 h, nicht rückgängig (endgültige Prüfung macht B2) | über Endpunkt-Tests | 20+80 |
| `src/FoodAssistant.Infrastructure/Activity/ActivityFeedStore.cs` | neu | EF mit Join auf Mitgliedschaften/Users (Anzeigename) und Produkte | – | 90 |
| Registrierungen (Stubs Activity) | ändern | | – | +2 |
| `src/FoodAssistant.Server/Activities/ActivityEndpoints.cs` | ändern (Stub) | `GET /activity?area&cursor&limit` (Any) | `ActivityEndpointTests` (Reihenfolge, Cursor, Person/ehemalig/System, fremder Haushalt 404) | 40+160 |
| `src/FoodAssistant.Client/Activity/*`, Registrierung | neu | `ListAsync` | – | 50 |
| `src/FoodAssistant.UI/Activity/ActivityPage.razor` + `ActivityText.cs` + resx | neu | `@page "/activity"`: nach Tag gruppiert, Filter Vorrat/Einkauf, „Mehr laden“, „Rückgängig“ bei `CanUndo` (ruft `IInventoryClient.UndoAsync`), Live-Aktualisierung | `ActivityPageTests` | 170+5+2×50 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Verlauf“ | – | +5 |
| `docs/kb/telemetrie.md`, `openapi-v1.json` | ändern | Ablauf `foodassistant.activity.list` | – | – |

**Akzeptanz:** Tests grün; Sichtprüfung: Aktionen aus Vorrat und Liste erscheinen als Sätze, Rückgängig wirkt.

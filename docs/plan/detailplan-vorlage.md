# Detailplan – Vorlage

Regeln: [AGENTS.md §13](../../AGENTS.md). Ein Detailplan zerlegt einen Feature- oder Spurplan in
**Arbeitspakete**, die ein Modell der zugewiesenen Stufe **ohne eigene Designentscheidung und ohne
breite Codesuche** umsetzen kann. Er steht in der Feature-Datei im Abschnitt „Detailplan“ (bei mehr
als etwa 15 Dateien als eigene Datei `docs/plan/features/<id>-detail.md`).

## Vorgehen

1. **Einschätzen** (§13.1) je Aufgabenpunkt: Umfang, Risiko, Neuheit, Klarheit, Prüfbarkeit → Stufe 1–4.
2. **Offene Fragen zuerst klären** (Nutzer, Grilling). Ein Paket mit offener Frage ist
   **blockiert** und nennt die Frage (z. B. „blockiert durch F3“).
3. Pakete gleicher Stufe, die dieselben Dateien berühren, zu **einem** Paket zusammenfassen.
4. Jede Datei des Pakets in die Tabelle; Muster immer mit **konkreter bestehender Datei** nennen.
5. Akzeptanz so formulieren, dass ein Befehl oder Test sie belegt.

## Stufen und Modelle (§13.3)

| Stufe | Modell | Effort | Typisch |
|---|---|---|---|
| 1 – mechanisch | Haiku | `low` | Suchen, Texte DE/EN, Katalog-/Plan-Häkchen, Log-Definitionen nach Vorgabe, Umbenennen, Aufteilen nach Vorgabe |
| 2 – Standard nach Plan | Sonnet | `medium` | Domain-Logik mit Tests nach Plan, DTOs/Mapping, CRUD und UI nach Muster, Build-/Testfehler |
| 3 – anspruchsvoll | Opus | `high` | Datenmodell/Migration, Sync/Offline, Auth/Sicherheit, KI, Haltbarkeitsregeln, unklare Fehler, Merge, **jedes Review** |
| 4 – Grenzfall | Fable | `high` | Spikes mit offenem Ausgang, ADRs großer Tragweite, Fälle, an denen Opus scheiterte |

**Eskalation:** Zweimal rot am selben Paket oder eine Entscheidung, die der Plan nicht trifft →
aufhören, melden. Kein Raten, kein Umgehen.

---

## Paket-Vorlage (kopieren)

### Paket <Spur>.<n> – <Titel>

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** <Paket, Merge oder offene Frage – oder „–“>
- **Blockiert durch:** <F<n> aus m01-mvp.md „Offene Fragen“ – oder „–“>
- **Branch:** `feat/mNN-<id>-…`
- **Muster:** <bestehende Datei(en), denen gefolgt wird, z. B. `src/FoodAssistant.Server/Households/MemberEndpoints.cs`>

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/…/Foo.cs` | neu | `public sealed class Foo(IBarStore store, IHouseholdContext household, ILogger<Foo> logger)`; `Task<OperationResult<FooDto>> CreateAsync(Guid householdId, CreateFooRequest request, CancellationToken ct)` – Ablauf in Stichpunkten | `FooTests.Create_*` | 120 |
| `src/…/FooLog.cs` | neu | `[LoggerMessage]`: `FooCreated` (EventId N, Information, `FooId`, `HouseholdId`) | – | 20 |

**Reihenfolge:** 1. Domain + Tests → 2. Application → 3. Infrastructure → 4. Endpunkte → 5. Tests →
6. OpenAPI-Snapshot → 7. KB/Katalog.

**Telemetrie:**
- Abläufe: `foodassistant.<feature>.<vorgang>` (Attribute: nur IDs, Zählwerte, Kategorien, Status)
- Logs: Name / EventId / Level / Attribute
- Messwerte: `foodassistant.<feature>.<messwert>` (Art, Einheit, Attribute)
- Eintrag in `docs/kb/telemetrie.md` in derselben Änderung.

**Fehlercodes:** `<feature>.<code>` mit Status (ADR-0004).

**Akzeptanz:**
- `dotnet build FoodAssistant.slnx` ohne Warnungen
- `dotnet test --solution FoodAssistant.slnx` grün, insbesondere `<Testklassen>`
- `./scripts/check-file-length.ps1` grün
- <fachlicher Nachweis, z. B. „fremder Haushalt → 404“ als Test, Sichtprüfung im Browser-Pane>

**Nicht Teil des Pakets:** <Abgrenzung, damit niemand nebenbei mehr baut>

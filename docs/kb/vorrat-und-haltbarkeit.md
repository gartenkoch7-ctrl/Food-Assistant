# Vorrat und Haltbarkeit

Grundsatz: [ADR-0010](../adr/0010-vorratsmodell-und-haltbarkeit.md). Begriffe: [glossar.md](glossar.md).

## Modell

```
Haushalt ─┬─ Produkt (Katalog des Haushalts)
          │     ├─ Barcodes (0..n), Kategorie, Standardeinheit, Mindestbestand
          │     ├─ Haltbarkeits-Überschreibungen (optional)
          │     └─ Charge (0..n)
          │           ├─ Menge (Quantity), Lagerort, Zustand
          │           ├─ Ablaufdatum (DateOnly) + Art: MHD | Verbrauchsdatum | keins
          │           └─ Herkunft: gescannt, manuell, Einkauf, Import, Rest von Rezept
          └─ Lagerort (frei benannt) + Typ: gekühlt | gefroren | Raumtemperatur
```

- **Produktkatalog pro Haushalt.** Open-Food-Facts-Daten liegen serverweit im Cache und werden
  beim Anlegen eines Produkts kopiert; Änderungen des Haushalts bleiben im Haushalt.
- **Bestand** eines Produkts = Summe seiner Chargen (gleiche Einheit bzw. umrechenbar).
- **Verbrauch nach FEFO** („first expired, first out“): ohne Auswahl wird die Charge mit dem
  frühesten Ablaufdatum genommen; Chargen ohne Datum zuletzt.

## Zustände einer Charge

| Zustand | Auslöser | Wirkung auf Ablaufdatum |
|---|---|---|
| verschlossen | Standard | Datum der Packung bzw. Regel |
| geöffnet | „Geöffnet“ antippen | `min(Packungsdatum, Öffnungsdatum + Regel „geöffnet“)` |
| eingefroren | Lagerort Typ gefroren oder „Einfrieren“ | Regel „gefroren“ ab Einfrierdatum; Packungsdatum ruht |
| aufgetaut | aus gefroren nach gekühlt/Raumtemperatur | Regel „aufgetaut“ ab Auftaudatum (kurz) |
| Rest/selbst gemacht | „Rest anlegen“ oder Kochen (M2) | Regel „Rest“ für Kategorie/Lagerort |

- **Teilverbrauch** ändert nur die Menge; Datum und Zustand bleiben. Mengen werden mit drei
  Dezimalen gespeichert: Eine Entnahme in anderer Einheit wird auf das nächste 0,001 der
  Chargeneinheit aufgerundet (höchstens der Rest). Bleibt weniger als 0,001 übrig, ist die Charge
  leer und wird gelöscht; das Ereignis heißt dann „aufgebraucht“, die Aktivität bleibt
  `consumed_partly`.
- Ein Zustandswechsel wird im Aktivitätsverlauf vermerkt und ist rückgängig zu machen.
- **Verschieben** auf einen Lagerort anderen Typs: in den Gefrierschrank = einfrieren (Regel
  „gefroren“ der Kategorie), heraus = auftauen (Regel „aufgetaut“ für den Ziel-Lagertyp), zwischen
  Kühlschrank und Vorratsschrank bleiben Datum und Zustand. Aufgetaut → gefroren lehnt der Server ab
  (422 `inventory.refreeze_not_allowed`).
- **Korrigieren** (Tippfehler, F14): Menge immer; Packungsdatum und Art nur, solange die Charge
  verschlossen ist (sonst 422 `inventory.date_locked`) – danach stammt das Datum aus einer Regel.
- Eine **leere Charge wird gelöscht** (Tombstone im Änderungsprotokoll); das Verbrauchsereignis bleibt.

## TK-Ware (neue Charge im Gefrierschrank, F5)

- **Mit Packungsdatum** = gekaufte Tiefkühlware: verschlossen, Lagertyp gefroren, das Packungsdatum
  gilt (kein Einfrierdatum).
- **Ohne Packungsdatum** oder mit Schalter **„selbst eingefroren“** = zu Hause eingefroren: ab heute
  Regel „gefroren“ (gilt als Verbrauchsdatum), ein angegebenes Packungsdatum ruht.
- Auftauen gilt für beide gleich (Regel „aufgetaut“ ab Auftaudatum).

## Rückgängig (24 h, F4)

- Jede Vorratsaktion (einbuchen, Rest, korrigieren, verschieben, öffnen, verbrauchen, entsorgen)
  schreibt einen Eintrag in `HouseholdActivities` mit dem Zustand **vorher** und dem `UpdatedAt`,
  das die Charge **nach** der Aktion trägt (`BatchSnapshotJson`, eigenes Format, Version 1).
- Rückgängig (`POST …/inventory/activities/{id}/undo`, Rolle Mitglied) geht, wenn die Aktion
  höchstens 24 Stunden alt, noch nicht rückgängig gemacht und jede betroffene Charge seitdem
  unverändert ist (gleiches `UpdatedAt`) bzw. noch gelöscht ist. Sonst 409 `inventory.undo_not_possible`.
- Wirkung: Zustand und Lagerort zurück, gelöschte Chargen mit **gleicher Id** neu, beim Einbuchen
  die Charge wieder weg; Verbrauchsereignisse und die Aktivität bekommen `UndoneAt`; eine neue
  Aktivität `undone` (selbst nicht rückgängig zu machen).
- `UpdatedAt` wird auf Mikrosekunden gekürzt, weil PostgreSQL `timestamptz` so speichert und der
  Vergleich sonst nie gleich wäre.

## Haltbarkeitsregeln

- Food-Assistant liefert **Standardregeln pro Kategorie** mit (Tage nach Öffnen, im Gefrierfach,
  nach Auftauen, als Rest – jeweils je Lagerort-Typ). Jede Regel nennt ihre **Quelle**
  (z. B. Verbraucherzentrale, BZfE); die Tabelle liegt versioniert im Repository.
- Haushalte können pro Kategorie oder Produkt **überschreiben**; die UI zeigt, ob ein Wert
  Standard oder eigen ist.
- Die **KI schlägt nie** Haltbarkeiten vor (AGENTS.md §2.3); sie darf Regeln nur zitieren.
- Fehlt eine Regel, gibt es kein berechnetes Datum – die Charge ist „ohne Datum“.
- Ein **aus einer Regel berechnetes Datum gilt als Verbrauchsdatum** (rot, Warnung, kein Hinweis
  „oft noch gut“) – auch wenn die Packung nur ein MHD trägt. Entschieden 2026-11-18.
- **Kein Wiedereinfrieren** nach dem Auftauen: aufgetaut bleibt nur verbrauchen, entsorgen oder als
  Rest erfassen. Entschieden 2026-11-18.

## Status und Formulierungen

| Status | Bedingung | Text (Beispiele) |
|---|---|---|
| abgelaufen | Datum < heute | „seit gestern abgelaufen“, „seit 3 Tagen abgelaufen“ |
| läuft bald ab | heute ≤ Datum ≤ heute + Vorlauf (Standard 2 Tage) | „heute“, „morgen“, „noch 2 Tage“ |
| haltbar | Datum > heute + Vorlauf | „bis 24.11.“ (ab 7 Tagen nur Datum) |
| ohne Datum | kein Datum | „kein Datum“ |

- **„Heute“** ist der Kalendertag in der Zeitzone des Haushalts.
- **MHD abgelaufen:** „Mindesthaltbarkeit überschritten – oft noch gut. Riech und schau, bevor du
  es wegwirfst.“
- **Verbrauchsdatum abgelaufen:** „Verbrauchsdatum überschritten – nicht mehr essen.“
- Bei Verbrauchsdatum wird der Status auch im zugänglichen Namen genannt.

## Verbrauch und Entsorgung

- Aktionen: **aufgebraucht**, **teilweise verbraucht** (Menge), **entsorgt** mit Grund
  (abgelaufen, verdorben, schmeckt nicht, zu viel gekocht, sonstiges).
- Jede Aktion erzeugt ein **Verbrauchsereignis** (Produkt, Menge, Art, Grund, Person, Zeit) – Basis
  für Statistik, Verlauf und Mindestbestand. Je betroffener Charge ein Ereignis, Menge in der
  Einheit der Charge, mit dem Ablaufstatus im Moment des Verbrauchs (Haushalts-„heute“ und Vorlauf).
- **Produkt verbrauchen** verteilt nach FEFO; bei gleichem Datum zuerst die früher eingebuchte
  Charge. Ohne Menge („aufgebraucht“, „entsorgt“) wird die ganze früheste Charge genommen. Reicht
  der Bestand nicht: 422 `inventory.not_enough_stock` (die verfügbare Menge liest der Client aus
  `GET …/inventory/products/{id}`).
- Nach jeder Bestandsänderung ruft der Server alle `IStockChangedHandler` (Mindestbestand); ein
  Fehler dort wird geloggt und macht die Aktion nicht ungültig.
- Fällt der Bestand unter den **Mindestbestand**, siehe [einkauf.md](einkauf.md).

## Statistik (MVP)

- Pro Monat: verbraucht vs. entsorgt (Anzahl, Menge), Gründe, häufigste entsorgte Produkte, Trend.
- Haushaltsweit, nicht pro Person. Geldwert ab M5.

## Etiketten (MVP: PDF)

- Für Chargen (v. a. eingefroren, Reste): Name, Datum eingefroren/zubereitet, Ablaufdatum, QR-Code.
- QR-Code enthält nur eine Kennung der Charge (keine Daten); Scannen öffnet die Charge in der App.
- Formate: gängige Etikettenbögen (Größen konfigurierbar); Labeldrucker ab M5.

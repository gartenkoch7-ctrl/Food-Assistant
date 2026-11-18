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

- **Teilverbrauch** ändert nur die Menge; Datum und Zustand bleiben.
- Ein Zustandswechsel wird im Aktivitätsverlauf vermerkt und ist rückgängig zu machen.

## Haltbarkeitsregeln

- Food-Assistant liefert **Standardregeln pro Kategorie** mit (Tage nach Öffnen, im Gefrierfach,
  nach Auftauen, als Rest – jeweils je Lagerort-Typ). Jede Regel nennt ihre **Quelle**
  (z. B. Verbraucherzentrale, BZfE); die Tabelle liegt versioniert im Repository. Werte, Quellen und
  Freigabe: [haltbarkeitsregeln.md](haltbarkeitsregeln.md).
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
  für Statistik, Verlauf und Mindestbestand.
- Fällt der Bestand unter den **Mindestbestand**, siehe [einkauf.md](einkauf.md).

## Statistik (MVP)

- Pro Monat: verbraucht vs. entsorgt (Anzahl, Menge), Gründe, häufigste entsorgte Produkte, Trend.
- Haushaltsweit, nicht pro Person. Geldwert ab M5.

## Etiketten (MVP: PDF)

- Für Chargen (v. a. eingefroren, Reste): Name, Datum eingefroren/zubereitet, Ablaufdatum, QR-Code.
- QR-Code enthält nur eine Kennung der Charge (keine Daten); Scannen öffnet die Charge in der App.
- Formate: gängige Etikettenbögen (Größen konfigurierbar); Labeldrucker ab M5.

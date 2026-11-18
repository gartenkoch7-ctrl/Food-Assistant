# Haltbarkeitsregeln – mitgelieferte Standardwerte

> **Freigegeben vom Nutzer am 2026-11-18** (Entscheidung F2, [m01-mvp.md](../plan/m01-mvp.md)), zusammen
> mit Format Version 2 (Quelle je Wert). Jede Änderung an Werten oder Quellen braucht eine neue Freigabe
> und einen neuen Eintrag hier.

Daten: `src/FoodAssistant.Infrastructure/ShelfLife/default-shelf-life-rules.json` (eingebettete
Ressource, geladen von `EmbeddedDefaultShelfLifeRules` beim Start). Grundlagen:
[vorrat-und-haltbarkeit.md](vorrat-und-haltbarkeit.md), ADR-0010, AGENTS.md §2.3.

## Grundsätze

- **Nur geprüfte öffentliche Quellen** (BZfE, Verbraucherzentrale, BfR), am 2026-11-18 abgerufen und
  gelesen. Übernommen werden nur Zahlen, keine Texte. Was sich nicht belegen ließ, fehlt.
- **Vorsichtig:** bei Spannen die **untere Grenze**; ein Monat = **30 Tage**.
- **Fehlt ein Wert, gibt es kein berechnetes Datum** (Charge „ohne Datum“).
- Ein aus einer Regel berechnetes Datum gilt als **Verbrauchsdatum**.
- **Kein Wiedereinfrieren** nach dem Auftauen: gefrorene Zeilen haben keinen Wert „nach dem Auftauen“.
- Die **KI schlägt nie** Haltbarkeiten vor; sie darf diese Regeln nur zitieren.
- **Welche Zeile gilt:** Öffnen → Zeile des aktuellen Lagertyps; Einfrieren → Zeile `Frozen`;
  Auftauen → Zeile des Ziel-Lagertyps; Rest → Zeile des Lagertyps des Ziel-Lagerorts.
- Haushalte überschreiben je Kategorie oder Produkt (je Wert); die UI zeigt „Standard“/„Eigen“ und
  die Quellen.

## Format (Version 2)

```json
{ "version": 2,
  "sources": { "bzfe_milch": { "title": "…", "url": "https://…", "retrieved": "2026-11-18" } },
  "rules": [ { "category": "dairy", "storageType": "Chilled",
               "daysAfterOpening": { "days": 3, "source": "bzfe_milch" },
               "daysFrozen": null, "daysAfterThawing": null, "daysAsLeftover": null } ] }
```

Der Parser ist streng: Er prüft bekannte Kategorien, keine doppelten Zeilen, 0–3650 Tage, nur
https-Quellen und dass jeder Wert eine bekannte Quelle nennt. Jede Quelle muss zitiert werden, alle
vier Schlüssel sind Pflicht, unbekannte Felder sind verboten. Ein Fehler stoppt den Start. Version 1
(eine Quelle je Zeile) wurde nie mit Daten ausgeliefert und wird nicht mehr gelesen.

## Quellen

| Schlüssel | Quelle | Stand der Seite |
|---|---|---|
| `bzfe_milch` | BZfE: [Milch: Vom Einkauf in die Küche](https://www.bzfe.de/kueche-und-alltag/vom-acker-bis-zum-teller/milch/vom-einkauf-in-die-kueche) | 29.06.2026 |
| `bzfe_schwein` | BZfE: [Schweinefleisch: Vom Einkauf in die Küche](https://www.bzfe.de/kueche-und-alltag/vom-acker-bis-zum-teller/schweinefleisch/schweinefleisch-vom-einkauf-in-die-kueche) | 02.12.2025 |
| `bzfe_fisch` | BZfE: [Fisch und Meeresfrüchte: Vom Einkauf in die Küche](https://www.bzfe.de/kueche-und-alltag/vom-acker-bis-zum-teller/fisch-und-meeresfruechte/fisch-vom-einkauf-in-die-kueche) | 14.04.2026 |
| `bzfe_eier` | BZfE: [Kann man Eier einfrieren?](https://www.bzfe.de/presse/pressemeldungen-archiv-2024-und-frueher/kann-man-eier-einfrieren) | 29.03.2023 |
| `bzfe_huelsenfruechte` | BZfE: [How-to: Hülsenfrüchte](https://www.bzfe.de/kueche-und-alltag/kochen/how-to-obst-und-gemuese/how-to-huelsenfruechte) | 31.03.2025 |
| `bzfe_hygiene` | BZfE: [Hygiene (Einfache Sprache)](https://www.bzfe.de/einfache-sprache/kochen-und-aufbewahren/hygiene) | 13.08.2026 |
| `vz_nds_einfrieren` | Verbraucherzentrale Niedersachsen: [Tipps und Tricks beim Einfrieren und Auftauen](https://www.verbraucherzentrale-niedersachsen.de/wissen/lebensmittel/gesund-ernaehren/tipps-und-tricks-beim-einfrieren-und-auftauen-120371) | 26.09.2025 |
| `bfr_gefluegel` | BfR: [Ausgewählte Fragen und Antworten zu Geflügelfleisch](https://www.bfr.bund.de/cm/343/ausgewaehlte-fragen-und-antworten-zu-gefluegelfleisch.pdf) | FAQ 28.04.2021 |
| `bfr_privathaushalt` | BfR: [Verbrauchertipps: Schutz vor Lebensmittelinfektionen im Privathaushalt](https://www.bfr.bund.de/cm/350/verbrauchertipps_schutz_vor_lebensmittelinfektionen_im_privathaushalt.pdf) | 2020 |

## Tabelle (29 Zeilen, 40 Werte)

Werte in Tagen mit Quelle; „–“ = kein Wert. Spalten: Öffnen / Gefroren / Auftauen / Rest.

| Kategorie | Lagertyp | Öffnen | Gefroren | Auftauen | Rest |
|---|---|---|---|---|---|
| dairy | Chilled | 3 `bzfe_milch` | – | – | – |
| dairy | Frozen | – | 60 `vz_nds_einfrieren` | – | – |
| cheese | Frozen | – | 60 `vz_nds_einfrieren` | – | – |
| eggs | Frozen | – | 240 `bzfe_eier` | – | – |
| meat | Chilled | 1 `bzfe_schwein` | – | 0 `bfr_privathaushalt` | 2 `bzfe_hygiene` |
| meat | Frozen | – | 30 `vz_nds_einfrieren` | – | 30 `vz_nds_einfrieren` |
| meat | Ambient | – | – | – | 0 `bzfe_hygiene` |
| poultry | Chilled | – | – | 0 `bfr_gefluegel` | 2 `bzfe_hygiene` |
| poultry | Frozen | – | 30 `vz_nds_einfrieren` | – | 30 `vz_nds_einfrieren` |
| poultry | Ambient | – | – | – | 0 `bzfe_hygiene` |
| fish | Chilled | 0 `bzfe_fisch` | – | 0 `bfr_privathaushalt` | 2 `bzfe_hygiene` |
| fish | Frozen | – | 30 `vz_nds_einfrieren` | – | 30 `vz_nds_einfrieren` |
| fish | Ambient | – | – | – | 0 `bzfe_hygiene` |
| sausage | Frozen | – | 30 `vz_nds_einfrieren` | – | – |
| fruit | Frozen | – | 270 `vz_nds_einfrieren` | – | – |
| vegetables | Chilled | – | – | – | 2 `bzfe_hygiene` |
| vegetables | Frozen | – | 180 `vz_nds_einfrieren` | – | 30 `vz_nds_einfrieren` |
| vegetables | Ambient | – | – | – | 0 `bzfe_hygiene` |
| herbs | Frozen | – | 90 `vz_nds_einfrieren` | – | – |
| bread | Frozen | – | 30 `vz_nds_einfrieren` | – | – |
| grains | Chilled | – | – | – | 2 `bzfe_hygiene` |
| grains | Frozen | – | – | – | 30 `vz_nds_einfrieren` |
| grains | Ambient | – | – | – | 0 `bzfe_hygiene` |
| legumes | Chilled | – | – | – | 2 `bzfe_hygiene` |
| legumes | Frozen | – | 270 `bzfe_huelsenfruechte` | – | 30 `vz_nds_einfrieren` |
| legumes | Ambient | – | – | – | 0 `bzfe_hygiene` |
| ready_meals | Chilled | – | – | – | 2 `bzfe_hygiene` |
| ready_meals | Frozen | – | 30 `vz_nds_einfrieren` | – | 30 `vz_nds_einfrieren` |
| ready_meals | Ambient | – | – | – | 0 `bzfe_hygiene` |

**Belege (sinngemäß):** geöffnete Milch jeder Sorte in etwa drei Tagen verbrauchen (`bzfe_milch`).
Eingefroren halten Milchprodukte 2–6, Fleisch/Fisch 1–12, Wurst 1–6, Obst 9–12, Gemüse 6–12 Monate,
Kräuter 3–4, Backwaren 1–3 und Selbstgekochtes 1–3 Monate (`vz_nds_einfrieren`). Eier ohne Schale
halten bei −18 °C höchstens 8–12 Monate (`bzfe_eier`), tiefgekühlte Hülsenfrüchte 9–15 Monate
(`bzfe_huelsenfruechte`). Schweinehack innerhalb eines Tages verbrauchen (`bzfe_schwein`). Frischer
Fisch am Einkaufstag, notfalls am nächsten Tag (`bzfe_fisch`). Zubereitete Speisen bis zu zwei Tage im
Kühlschrank; was nicht gekühlt werden kann, am selben Tag verbrauchen (`bzfe_hygiene`; BfR nennt zwei
bis drei Tage). In der Mikrowelle aufgetaute rohe tierische Lebensmittel sofort verarbeiten
(`bfr_privathaushalt`). Aufgetautes Geflügel umgehend zubereiten (`bfr_gefluegel`).

## Auslegungen (mit der Tabelle freigegeben)

1. **Rest bei Raumtemperatur = 0** (selber Tag), abgeleitet aus „nicht gekühlt lagerbare Lebensmittel
   noch am selben Tag verbrauchen“.
2. **Fleisch geöffnet = 1 Tag.** Die Quellen nennen Fristen für frisch gekaufte Ware. Als Obergrenze
   nach dem Öffnen gilt die kürzeste belegte Frist der Kategorie (Hackfleisch: innerhalb eines Tages).
   Lose gekauftes Hack soll laut BZfE innerhalb von 6–8 Stunden verbraucht werden → per
   Produkt-Überschreibung 0 setzen.
3. **Fisch geöffnet = 0**, Begründung wie 2 (untere Grenze von „am Einkaufstag, notfalls nächster Tag“).
4. **Milchprodukte geöffnet = 3** gilt auch für Joghurt, Sahne und Butter (vorsichtig, oft kürzer als nötig).
5. **Gefrierzeiten** sind Qualitätsgrenzen, kein Sicherheitsrisiko. Trotzdem gilt die untere Grenze,
   damit das Verbrauchsdatum nie zu spät liegt.
6. **Eier gefroren = 240 Tage** gilt laut Quelle nur für Eier ohne Schale; die Regel greift trotzdem für
   jede gefrorene Charge der Kategorie `eggs`.

Dazu: **Auftauen = 0 für Fleisch und Fisch** überträgt die BfR-Aussage zum Auftauen in der Mikrowelle
auf jedes Auftauen (vorsichtig; Geflügel ist mit `bfr_gefluegel` direkt belegt).

## Geprüft, aber nicht übernommen

- **Käse ~1 Woche im Kühlschrank** (Verbraucherzentrale): unklar, ob geöffnet gemeint ist; Weich- und
  Hartkäse unterscheiden sich stark.
- **Pflanzendrinks geöffnet bis 2 Wochen** (Verbraucherzentrale): gilt nur für Drinks, `plant_based`
  umfasst auch Tofu u. Ä.
- **Saft geöffnet „wenige Tage“**: keine Zahl. **Geöffnete Konserven ca. 3 Tage**: nur aus
  Sekundärquellen bzw. einem nicht abrufbaren Flyer von 2012.
- **Frisches Schweinefleisch 2–3 Tage, Geflügel 5–6 Tage**: als Wert nach dem Öffnen zu lang;
  die Geflügelangabe gilt für ganze Tiere ab Schlachtung.
- **Längere Gefrierzeiten** (BZfE: Rind/Geflügel 9–12, Fisch/fettes Fleisch 6–9 Monate; BfR: Hähnchen
  4–6 Monate): bewusst die kürzere Spanne der Verbraucherzentrale.
- **Hartgekochte Eier 2–4 Wochen** (BZfE): passt auf keinen der vier Werte.
- **Ohne Werte:** `baking`, `beverages`, `canned`, `frozen` (gekaufte TK-Ware: Datum der Packung),
  `oils`, `other`, `plant_based`, `sauces`, `spices`, `sweets`.

## Änderungen

Neue oder geänderte Werte: Quelle prüfen und hier eintragen, dann die JSON ändern. Der Test
`DefaultShelfLifeRulesTests.ShippedTable_IsTheApprovedTable` (Zeilen-, Werte- und Quellenzahl) ist
anzupassen. **Nur nach Freigabe durch den Nutzer**, mit Datum in der Kopfzeile.

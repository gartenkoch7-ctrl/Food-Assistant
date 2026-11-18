# M1 B1.1 – Entwurf der Standard-Haltbarkeitsregeln (zur Freigabe)

> **Status: ENTWURF – nicht aktiv.** Die mitgelieferte Tabelle
> `src/FoodAssistant.Infrastructure/ShelfLife/default-shelf-life-rules.json` bleibt leer
> (`"rules": []`), bis der Nutzer diesen Entwurf freigibt (Entscheidung F2, [m01-mvp.md](../m01-mvp.md)).
> Ohne Regel gibt es kein berechnetes Datum – das ist der sichere Rückfall.

Daten des Entwurfs: [m01-b1-standardwerte-entwurf.json](m01-b1-standardwerte-entwurf.json) (Format
Version 1 wie die eingebettete Tabelle). `DefaultShelfLifeRulesTests.DraftTable_IsValidAndCitesOnlyListedSources`
prüft ihn mit demselben strengen Parser wie die echte Tabelle.

Erstellt 2026-11-18 von Spur B1 (Opus). Alle Quellen an diesem Tag abgerufen und gelesen.

## Grundsätze des Entwurfs

- **Nur geprüfte öffentliche Quellen** (BZfE, Verbraucherzentrale, BfR). Jeder Wert ist eine Zahl aus
  der genannten Seite; Texte wurden nicht übernommen. Was sich nicht belegen ließ, fehlt.
- **Vorsichtig:** Bei Spannen gilt die **untere Grenze**; Monate werden mit **30 Tagen** gerechnet.
- Ein aus einer Regel berechnetes Datum gilt als **Verbrauchsdatum** (KB vorrat-und-haltbarkeit).
- **Kein Wiedereinfrieren** nach dem Auftauen (Entscheidung 2026-11-18); deshalb gibt es keine
  Regel „nach dem Auftauen“ in gefrorenen Zeilen.
- Die **KI schlägt nie** Haltbarkeiten vor (AGENTS.md §2.3); diese Tabelle stammt nicht von einer KI-Ausgabe,
  sondern aus den Quellen unten.
- **Welche Zeile gilt** (Feature-Datei B1): Öffnen → Zeile des aktuellen Lagertyps; Einfrieren → Zeile
  `Frozen`; Auftauen → Zeile des Ziel-Lagertyps; Rest → Zeile des Ziel-Lagerorts.

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
| `bfr_privathaushalt` | BfR: [Verbrauchertipps: Schutz vor Lebensmittelinfektionen im Privathaushalt](https://www.bfr.bund.de/cm/350/verbrauchertipps_schutz_vor_lebensmittelinfektionen_im_privathaushalt.pdf) | 2020 (nur Stützung, in der JSON nicht zitiert) |

## Tabelle (29 Zeilen, alle in der JSON)

Spalten: Öffnen / Gefroren / Auftauen / Rest in Tagen; „–“ = kein Wert (kein berechnetes Datum).

| Kategorie | Lagertyp | Öffnen | Gefroren | Auftauen | Rest | Quelle | Beleg (sinngemäß) |
|---|---|---|---|---|---|---|---|
| dairy | Chilled | 3 | – | – | – | `bzfe_milch` | Geöffnete Milch jeder Sorte in etwa drei Tagen verbrauchen |
| dairy | Frozen | – | 60 | – | – | `vz_nds_einfrieren` | Milchprodukte eingefroren 2–6 Monate → 2 |
| cheese | Frozen | – | 60 | – | – | `vz_nds_einfrieren` | Käse als Milchprodukt: 2–6 Monate → 2 |
| eggs | Frozen | – | 240 | – | – | `bzfe_eier` | Ohne Schale bei −18 °C höchstens 8–12 Monate → 8 |
| meat | Chilled | 1 | – | – | – | `bzfe_schwein` | Hackfleisch innerhalb eines Tages verbrauchen (Stücke 2–3 Tage; vorsichtig: Hack) |
| meat | Frozen | – | 30 | – | 30 | `vz_nds_einfrieren` | Fleisch/Fisch 1–12 Monate → 1; Selbstgekochtes 1–3 Monate → 1 |
| meat | Ambient | – | – | – | 0 | `bzfe_hygiene` | Nicht gekühlt lagerbare Lebensmittel noch am selben Tag verbrauchen |
| poultry | Chilled | – | – | 0 | – | `bfr_gefluegel` | Aufgetautes Geflügel umgehend zubereiten |
| poultry | Frozen | – | 30 | – | 30 | `vz_nds_einfrieren` | wie meat (BfR nennt für Hähnchen 4–6 Monate; VZ ist vorsichtiger) |
| poultry | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |
| fish | Chilled | 0 | – | – | – | `bzfe_fisch` | Frischer Fisch am Einkaufstag, notfalls am nächsten Tag → 0 |
| fish | Frozen | – | 30 | – | 30 | `vz_nds_einfrieren` | Fleisch/Fisch 1–12 Monate → 1; Selbstgekochtes → 1 |
| fish | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |
| sausage | Frozen | – | 30 | – | – | `vz_nds_einfrieren` | Wurst 1–6 Monate → 1 |
| fruit | Frozen | – | 270 | – | – | `vz_nds_einfrieren` | Obst 9–12 Monate → 9 |
| vegetables | Chilled | – | – | – | 2 | `bzfe_hygiene` | Zubereitete Speisen bis zu zwei Tage im Kühlschrank (BfR: zwei bis drei Tage) |
| vegetables | Frozen | – | 180 | – | 30 | `vz_nds_einfrieren` | Gemüse 6–12 Monate → 6; Selbstgekochtes → 1 |
| vegetables | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |
| herbs | Frozen | – | 90 | – | – | `vz_nds_einfrieren` | Kräuter 3–4 Monate → 3 |
| bread | Frozen | – | 30 | – | – | `vz_nds_einfrieren` | Backwaren 1–3 Monate → 1 |
| grains | Chilled | – | – | – | 2 | `bzfe_hygiene` | wie vegetables/Chilled |
| grains | Frozen | – | – | – | 30 | `vz_nds_einfrieren` | Selbstgekochtes 1–3 Monate → 1 |
| grains | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |
| legumes | Chilled | – | – | – | 2 | `bzfe_hygiene` | wie vegetables/Chilled |
| legumes | Frozen | – | 270 | – | – | `bzfe_huelsenfruechte` | Tiefgekühlte Hülsenfrüchte 9–15 Monate → 9 |
| legumes | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |
| ready_meals | Chilled | – | – | – | 2 | `bzfe_hygiene` | wie vegetables/Chilled |
| ready_meals | Frozen | – | 30 | – | 30 | `vz_nds_einfrieren` | Selbstgekochtes 1–3 Monate → 1 |
| ready_meals | Ambient | – | – | – | 0 | `bzfe_hygiene` | wie meat |

## Belegt, aber nicht in der JSON (Formatgrenze)

Format Version 1 erlaubt **eine Quelle je Zeile**. Diese Werte stammen aus einer zweiten Quelle für
eine Zeile, die schon eine andere Quelle hat. Sie fehlen deshalb in der JSON (sicher: ohne Wert kein Datum).

| Kategorie | Lagertyp | Wert | Quelle | Beleg |
|---|---|---|---|---|
| meat | Chilled | Rest 2 | `bzfe_hygiene` | Zubereitete Speisen bis zu zwei Tage |
| meat | Chilled | Auftauen 0 | `bfr_privathaushalt` | In der Mikrowelle aufgetaute rohe tierische Lebensmittel sofort verarbeiten (Auslegung für jedes Auftauen) |
| poultry | Chilled | Rest 2 | `bzfe_hygiene` | wie oben |
| fish | Chilled | Rest 2 | `bzfe_hygiene` | wie oben |
| fish | Chilled | Auftauen 0 | `bfr_privathaushalt` | wie meat |
| legumes | Frozen | Rest 30 | `vz_nds_einfrieren` | Selbstgekochtes 1–3 Monate |

## Geprüft, aber bewusst nicht übernommen

- **Käse, Kühlschrank ~1 Woche** (Verbraucherzentrale, „Tipps zur Haltbarkeit und Lagerung“): unklar, ob
  geöffnet gemeint ist; Weich- und Hartkäse unterscheiden sich stark.
- **Pflanzendrinks geöffnet bis 2 Wochen** (Verbraucherzentrale): gilt nur für Drinks; die Kategorie
  `plant_based` umfasst auch Tofu u. Ä. – zu riskant für die ganze Kategorie.
- **Saft geöffnet „wenige Tage“** (Verbraucherzentrale): keine Zahl.
- **Geöffnete Konserven ca. 3 Tage**: nur aus Sekundärquellen und einem Flyer von 2012, der nicht
  abrufbar war.
- **Frisches Schweinefleisch 2–3 Tage, Geflügel 5–6 Tage gekühlt**: betrifft ungeöffnete Ware (trägt
  ein Verbrauchsdatum), nicht „nach dem Öffnen“.
- **Längere Gefrierzeiten** (BZfE: Rind/Geflügel 9–12, Fisch/fettes Fleisch 6–9 Monate; BfR: Hähnchen
  4–6 Monate): bewusst die kürzere Spanne der Verbraucherzentrale genommen.
- **Hartgekochte Eier 2–4 Wochen** (BZfE): passt auf keinen der vier Werte.
- **Ohne belegte Werte:** `baking`, `beverages`, `canned`, `frozen` (gekaufte TK-Ware: Datum der
  Packung), `oils`, `other`, `plant_based`, `sauces`, `spices`, `sweets`, sowie `eggs`, `cheese`,
  `sausage`, `fruit`, `herbs`, `bread` außer „gefroren“.

## Auslegungen, die der Nutzer bestätigen sollte

1. **Rest bei Raumtemperatur = 0** (selber Tag), abgeleitet aus „nicht gekühlt lagerbare Lebensmittel
   noch am selben Tag verbrauchen“.
2. **Fleisch geöffnet = 1 Tag** (Hackfleisch-Regel für die ganze Kategorie). Lose gekauftes Hack soll
   laut BZfE innerhalb von 6–8 Stunden verbraucht werden → per Produkt-Überschreibung 0 setzen.
3. **Fisch geöffnet = 0** (untere Grenze von „am Einkaufstag, notfalls nächster Tag“).
4. **Milchprodukte geöffnet = 3** gilt auch für Joghurt, Sahne, Butter (vorsichtig, oft kürzer als nötig).
5. **Gefrierzeiten** sind Qualitätsgrenzen, kein Sicherheitsrisiko; trotzdem untere Grenze, damit das
   Verbrauchsdatum nie zu spät liegt.

## Freigabe – Ablauf

1. Nutzer prüft Tabelle, Auslegungen und Quellen; Antwort: „freigegeben“ (ggf. mit Änderungen) und
   Entscheidung, ob Format Version 2 (Quelle je Wert) kommt, damit die Werte aus „Formatgrenze“ dazukommen.
2. Danach (eigener Commit auf `feat/m01-b1-shelf-life` oder im Integrationsbranch):
   - Inhalt der Entwurfs-JSON nach `src/FoodAssistant.Infrastructure/ShelfLife/default-shelf-life-rules.json`.
   - Test `ShippedTable_StaysEmptyUntilTheUserApprovesTheDraft` durch eine Prüfung der Zeilenzahl ersetzen.
   - `docs/kb/haltbarkeitsregeln.md` anlegen (Tabelle, Quellen, **Datum der Freigabe**), in
     `docs/kb/README.md` verlinken; dieser Entwurf wird dann gelöscht oder als „übernommen“ markiert.
   - B1.1 in der Feature-Datei abhaken.

**Freigabe durch den Nutzer:** _offen_

# ADR-0010: Vorratsmodell und Haltbarkeit

- **Status:** Akzeptiert
- **Datum:** 2026-11-18

## Kontext

Der Vorrat ist der Kern des Produkts. Gleiche Produkte liegen oft mehrfach mit unterschiedlichen
Daten vor (zwei Milchpackungen, eine geöffnet). Öffnen, Einfrieren und Auftauen ändern die
Haltbarkeit. Haltbarkeitsaussagen sind sicherheitsrelevant (AGENTS.md §2.3).

## Entscheidung

- **Produkt + Chargen:** Produkte bilden den Katalog **pro Haushalt**; jede Charge hat Menge,
  Lagerort, Zustand, Ablaufdatum (`DateOnly`) und Art (MHD, Verbrauchsdatum, keins).
- **Open Food Facts** wird serverweit zwischengespeichert; beim Anlegen werden die Daten in den
  Haushaltskatalog kopiert.
- **Lagerorte** frei benannt, mit **Typ** (gekühlt, gefroren, Raumtemperatur); der Typ steuert die
  Regeln.
- **Zustände:** verschlossen, geöffnet, eingefroren, aufgetaut, Rest. Teilverbrauch ändert nur die
  Menge.
- **Haltbarkeitsregeln** werden **mitgeliefert** (pro Kategorie und Lagerort-Typ, mit Quelle,
  versioniert im Repository) und sind pro Kategorie/Produkt **überschreibbar**. Die KI schlägt nie
  Haltbarkeiten vor.
- **Verbrauch nach FEFO**; jede Aktion ist ein **Verbrauchsereignis** (Basis für Statistik,
  Mindestbestand, Verlauf).
- **Einkaufslisten** buchen erst bei **„Einkauf abschließen“** in den Vorrat ein.
- Berechnungen (Ablauf, Status, FEFO, Regeln, Mindestbestand) sind reine **Domain-Logik** mit
  vollständigen Unit-Tests.

## Konsequenzen

- Mehr Entitäten als bei einfachen Einträgen (Produkt, Charge, Ereignis, Regel); dafür sind
  Mindestbestand, Rezepte (M2) und Statistik ohne Umbau möglich.
- Die mitgelieferte Regeltabelle braucht Pflege und Quellenangaben; Änderungen daran sind
  fachliche Änderungen mit Review.
- Der Haushaltsexport (ADR-0007) muss Produkte, Chargen, Regel-Überschreibungen und Ereignisse
  enthalten.

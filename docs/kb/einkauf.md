# Einkauf

Sync- und Konfliktregeln: ADR-0005. Vorratsmodell: [vorrat-und-haltbarkeit.md](vorrat-und-haltbarkeit.md).

## Listen und Läden
- Ein Haushalt hat **beliebig viele Einkaufslisten**; eine davon ist die **Standardliste**
  (Ziel für Mindestbestand, Sprache, „auf die Liste“ ohne Angabe).
- Eine Liste kann einem **Laden** zugeordnet sein. Jeder Laden hat eine **Reihenfolge der
  Bereiche** (Obst & Gemüse, Kühlregal, …).
- Die Reihenfolge wird aus dem Abhaken **gelernt** (welcher Bereich wird typischerweise vor
  welchem abgehakt) und ist manuell anpassbar; manuelle Anpassungen haben Vorrang.
- Einträge: Produkt (verknüpft) oder Freitext, Menge, Notiz, Bereich, Herkunft (manuell,
  Mindestbestand, wiederkehrend, Essensplan, Sprache).

## Abhaken und Einkauf abschließen
1. Abhaken markiert nur „gekauft“ (offline möglich, ADR-0005).
2. **„Einkauf abschließen“** zeigt alle gekauften Einträge: pro Eintrag Menge, Lagerort und
   Ablaufdatum – vorbelegt aus Produkt-Standard, Regel oder letztem Kauf; mit einem Tipp änderbar.
3. Bestätigen legt die Chargen an und entfernt die Einträge von der Liste.
4. Freitext-Einträge werden beim Abschließen einem Produkt zugeordnet oder übersprungen.
- Ab M3 kann ein **Kassenbon** den Abschluss vorbefüllen (inkl. Preise).

## Mindestbestand
- Pro Produkt optional; Bestand unter Mindestbestand → Eintrag auf der Standardliste,
  markiert „automatisch“.
- Wird der Eintrag entfernt, kommt er erst beim **nächsten** Unterschreiten wieder.
- Steht das Produkt schon auf einer Liste, wird nichts doppelt angelegt.

## Wiederkehrende Artikel
- Regel: täglich, wöchentlich (Wochentag), monatlich (Tag); Ziel-Liste; Menge.
- Ein Hintergrunddienst legt den Eintrag zum Termin an (Zeitzone des Haushalts), nicht doppelt,
  wenn er noch offen ist.

## Import von Listen
- MVP: Grocy (Einkaufslisten), CSV.
- M5: **Bring!** nur per eingefügtem Text (eine Zeile pro Artikel, Menge wird erkannt).

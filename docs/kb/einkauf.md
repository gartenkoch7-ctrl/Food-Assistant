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
- **Standardliste ohne Namen:** Hat ein Haushalt beim Lesen der Listen keine Standardliste, legt
  der Server eine mit leerem `Name` an; die UI zeigt dann „Einkaufsliste“/„Shopping list“ in der
  Sprache des Lesers. Genau eine Standardliste je Haushalt (Unique-Index); eine andere Liste zur
  Standardliste machen setzt die alte in derselben Transaktion zurück. Die Standardliste kann weder
  gelöscht noch „entstandardisiert“ werden (409 `shopping.default_list`).
- Liste löschen löscht ihre Einträge (auch Tombstones) einzeln über den Context, damit das
  Änderungsprotokoll jeden Eintrag sieht; die Listenzeile ist dabei gesperrt (`FOR UPDATE`), damit
  kein neuer Eintrag an der Datenbank-Kaskade vorbei verschwindet.
- Eintrag mit Produkt: Text = Produktname, Bereich = Bereich des Produkts oder der Kategorie
  (`StoreSections.ForCategory`); Freitext ohne Bereich → `other`. Archivierte Produkte → 404.

## Rechte
- **Eingeschränkt: nur abhaken/ergänzen.** Eingeschränkte Mitglieder dürfen Einträge anlegen und
  abhaken (auch zurücknehmen). Ein `PUT` ist nur erlaubt, wenn sich Text, Bereich, Menge und Notiz
  nicht vom **aktuellen** Serverzustand unterscheiden; sonst 403 `shopping.restricted_change`.
  Löschen, Verschieben, Listen und Läden: ab Mitglied.

## Änderungen und Konflikte (API)
- Einträge werden mit **vollem Zielzustand** und `baseVersion` geändert (`PUT …/items/{id}`), gelöscht
  mit `DELETE …/items/{id}?baseVersion=n` – kein `If-Match`, weil die Konfliktregeln (ADR-0005,
  `ShoppingConflictResolver`) statt 412 entscheiden. Antwort immer `{ item, outcome }`.
- `ShoppingItems.Version` zählt die Anwendung bei jeder Änderung hoch (auch Verschieben, Löschen).
  `baseVersion` größer als die gespeicherte → 409 `shopping.unknown_version`.
- Text und Bereich folgen „letzte Änderung gewinnt“ wie Menge/Notiz: übernommen nur, wenn der
  eingehende Zustand gewinnt. Abhaken setzt `CheckedAt`/`CheckedByUserId`, Zurücknehmen löscht beide.
- Anlegen mit bekannter Id (auch gelöscht) → 200 mit dem bestehenden Eintrag (Warteschlange darf
  wiederholen); Id eines anderen Haushalts → 409 `common.conflict`.
- Verlauf (`ActivityArea.Shopping`): `item_added`, `item_checked`, `item_deleted`,
  `conflict_resolved` (Payload `{"outcome":"…"}`, auch wenn der Server seinen Stand behält).
- Zeitstempel der Einträge werden auf Mikrosekunden gekürzt (PostgreSQL-Genauigkeit), damit die
  Antwort einer Änderung dem späteren Lesen gleicht.

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

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
  Änderungsprotokoll jeden Eintrag sieht. Die Transaktion nimmt zuerst die Änderungsprotokoll-Sperre
  des Haushalts, dann die Listenzeile (`FOR UPDATE`): kein neuer Eintrag verschwindet an der
  Datenbank-Kaskade vorbei, ein gleichzeitiges Anlegen bekommt danach 404 `shopping.list_not_found`.
- Eintrag mit Produkt: Text = Produktname, Bereich = Bereich des Produkts oder der Kategorie
  (`StoreSections.ForCategory`); Freitext ohne Bereich → `other`. Archivierte Produkte → 404.

## Läden und Bereichsreihenfolge (F3)
- Feste Bereichsliste `StoreSections.All` (11 Schlüssel, Namen übersetzt die UI); je Laden wird
  nur die **Reihenfolge** gespeichert. `GET …/shopping/sections` = Standardreihenfolge ohne Laden.
- **Lernen:** Abhaken (offen → abgehakt) auf einer Liste mit Laden zählt den Übergang
  „Bereich des letzten Abhakens → aktueller Bereich“, wenn das letzte Abhaken in diesem Laden
  höchstens 2 h zurückliegt und der Bereich ein anderer ist (`SectionOrderLearner.IsSameTrip`).
  Danach merkt sich der Laden Bereich und Zeit (`LastCheckedSectionKey/At`).
- **Reihenfolge:** Punktzahl je Bereich = Ankünfte − Abgänge; aufsteigend sortiert, Gleichstand in
  Standardreihenfolge. Widersprüchliche Einkäufe heben sich auf, statt die Reihenfolge hin und her
  springen zu lassen. Manuelle Positionen kommen zuerst, der Rest folgt gelernt;
  `DELETE …/section-order` führt zur gelernten zurück.
- Das Abhaken ändert die Ladenzeile (`LastChecked*`, nur über den Context, ADR-0005-Regel) und
  damit die Version (ETag) des Ladens. Wer gleichzeitig den Laden umbenennt, bekommt 412 und lädt neu.
- Zwei gleichzeitige Abhaken im selben Laden: das zweite wird einmal mit dem neuen Stand wiederholt;
  verliert es erneut, wird nur dieser Übergang ausgelassen (das Abhaken selbst ist dann schon gespeichert).
- Laden löschen setzt `StoreId` der Listen über den Context auf `null` (Kaskade umgeht sonst das
  Änderungsprotokoll); Reihenfolge-Daten gehen per Datenbank-Kaskade mit (nicht synchronisiert).

## Rechte
- **Eingeschränkt: nur abhaken/ergänzen.** Eingeschränkte Mitglieder dürfen Einträge anlegen und
  abhaken (auch zurücknehmen). Ein `PUT` ist nur erlaubt, wenn sich Text, Bereich, Menge und Notiz
  nicht vom **aktuellen** Serverzustand unterscheiden; sonst 403 `shopping.restricted_change`.
  Einen gelöschten Eintrag dürfen sie nur abgehakt zurückholen („abgehakt gewinnt gegen gelöscht“),
  nicht unabgehakt.
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

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
- Gleichzeitiges Abhaken: Der Übergang wird unter der Änderungsprotokoll-Sperre des Haushalts
  gespeichert, und nur wenn das letzte Abhaken des Ladens noch das gelesene ist. Kam ein anderes
  dazwischen, wird `from` einmal mit dem neuen Stand entschieden; klappt auch das nicht, wird nur
  dieser Übergang ausgelassen (das Abhaken selbst ist dann schon gespeichert).
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
- **Regel** (`RestockDecision`, nach jeder Bestandsänderung und jeder Mindestbestand-Änderung über
  `IStockChangedHandler`): `Product.MinimumStockTriggered` ist der Merker „schon auf der Liste“.
  Unter Minimum und Merker aus: kein offener Eintrag → `AddToList` (Eintrag, Merker an); schon ein offener
  Eintrag (auch manuell) → nur `Arm` (Merker an, nichts doppelt). Merker an → nichts. Bestand ≥ Minimum oder
  Minimum entfernt, Merker an → `Disarm`. Ein gelöschter Eintrag setzt den Merker nicht zurück: erst
  ausreichender Bestand (z. B. nach „Einkauf abschließen“) macht wieder scharf.
- **Menge:** `Minimum − Bestand` in der Einheit des Mindestbestands; `piece`/`package`/`can` auf ganze Zahlen
  aufgerundet, sonst auf drei Nachkommastellen aufgerundet. Chargen in nicht umrechenbarer Einheit zählen nicht mit.
- Der Merker wird vor dem Anlegen gesetzt („Claim“): von zwei gleichzeitigen Prüfungen legt nur eine den Eintrag
  an; scheitert das Anlegen, wird er zurückgenommen. Das Setzen ändert die Version (`xmin`) des Produkts;
  Anlegen und Ändern eines Produkts liefern deshalb die Version **nach** den Handlern zurück.

## Wiederkehrende Artikel
- Regel: täglich, wöchentlich (Wochentag), monatlich (Tag); Ziel-Liste; Menge.
- Ein Hintergrunddienst legt den Eintrag zum Termin an (Zeitzone des Haushalts), nicht doppelt,
  wenn er noch offen ist.
- **API:** `GET/POST /shopping/recurring`, `PUT/DELETE /shopping/recurring/{ruleId}` (Lesen jede Rolle, Schreiben
  Mitglied, `If-Match` Pflicht bei PUT/DELETE). Unbekannte oder fremde Liste → 404 `shopping.list_not_found`,
  unbekanntes Produkt → 404 `catalog.product_not_found`, unbekannte Regel → 404 `restock.rule_not_found`.
  Eine Regel mit Produkt speichert dessen Namen als `text`; der Eintrag nutzt beim Lauf Name und Bereich des
  Produkts (archiviert → Freitext der Regel, Bereich `other`).
- **Termine** (`RecurrenceSchedule`, Haushaltsdaten): täglich = morgen; wöchentlich = nächster Wochentag **nach**
  dem Bezugstag; monatlich = nächster Tag `dom` nach dem Bezugstag, in kürzeren Monaten der letzte Tag. Erster
  Termin beim Anlegen/Ändern des Plans: `NextDue(…, gestern)`, also heute, wenn er passt. Ändern von Text, Menge
  oder Liste lässt den Termin unberührt. Wer den Plan an dem Tag ändert, an dem die Regel schon lief, kann
  für heute einen zweiten Eintrag bekommen (bewusst einfach gehalten).
- **Robustheit:** Der Eintrag eines Termins hat eine feste Id (Hash aus Regel-Id und Termin), ein Neustart
  zwischen Anlegen und Markieren legt ihn also nicht doppelt an. Eine Regel, die scheitert (Log 3556), rückt auf
  den nächsten Termin und stoppt die übrigen nicht; wurde sie während des Laufs bearbeitet, bleibt der Plan des
  Bearbeiters (Versionsvergleich).
- **Lauf** (`RecurringItemsJob`, alle 15 min, erster Lauf 30 s nach dem Start; je Haushalt ein Scope über
  `IHouseholdScopeFactory`, Fehler je Haushalt geloggt): Regeln mit `NextDueOn ≤ heute`; ist der zuletzt
  erzeugte Eintrag (`LastCreatedItemId`) noch offen (nicht abgehakt, nicht gelöscht), wird übersprungen, sonst
  ein Eintrag mit Herkunft „wiederkehrend“ angelegt. In beiden Fällen rückt `NextDueOn` auf den nächsten Termin
  nach heute (ein langer Ausfall legt also keine Einträge nach).
- **Oberfläche:** `/shopping/recurring` (Link auf „Mehr“), Anlegen/Bearbeiten/Löschen ab Mitglied; Produkt über
  `ProductPicker` oder Freitext.

## Import von Listen
- MVP: Grocy (Einkaufslisten), CSV.
- M5: **Bring!** nur per eingefügtem Text (eine Zeile pro Artikel, Menge wird erkannt).

## Oberfläche (D3)
- `/shopping` öffnet bei genau einer Liste direkt `/shopping/{id}` (Ersetzen im Verlauf); weitere Listen
  legt man auf der Listenseite an („Liste anlegen“), „Alle Listen“ erscheint ab zwei Listen.
- Die Listenseite gruppiert offene Einträge nach der Reihenfolge von `GetSectionOrderAsync(list.StoreId)`
  (ohne Laden: Standardreihenfolge); Überschriften je Bereich, abgehakte unter „Im Wagen (n)“ als
  zugeklapptes Aufklapp-Element. Namen der Bereiche kommen aus `Catalog/SectionText`.
- Abhaken ist optimistisch: Zeile kippt sofort, bei Fehler zurück und `ApiErrorAlert`; der Knopf wird
  nie deaktiviert (Fokus), eine zweite Betätigung während der Anfrage wird ignoriert. Eingeschränkte
  Mitglieder bekommen bei `shopping.restricted_change` (jemand hat Text/Menge/Notiz geändert) einmal
  einen Neuversuch auf dem frisch gelesenen Eintrag.
- Fremde Abhaker (offen → abgehakt zwischen zwei Ladevorgängen) werden über `ILiveAnnouncer` dezent
  angesagt. Der Server veröffentlicht die Änderung **vor** der HTTP-Antwort, die Live-Meldung eines eigenen
  Abhakens kommt also meist, während die Anfrage noch läuft: Die Seite merkt sich eigene Einträge
  (`OwnItemChanges`, 15 s wie `InventoryPage`) und sagt sie nicht als fremd an. Eigene Wechsel Liste ↔ Wagen
  werden stattdessen kurz angesagt („Milch im Wagen“), weil der Fokus mit der Zeile verschwindet.
- Die Bereichsreihenfolge wird nur beim ersten Laden, nach `shopping_list`/`store`-Änderungen und nach
  einem Resync geholt, nicht bei jeder Eintragsänderung. Ein Resync selbst sagt nichts an; eine ausstehende Ansage einer überholten Live-Ladung wird nachgeholt.
  Jedes Abhaken ändert die Version des Ladens (gelernte Felder liegen auf der Ladenzeile); eine Umbenennung bekommt
  daher oft 412. Der Laden-Dialog liest dann neu und wiederholt die Umbenennung einmal, solange der Name selbst
  unverändert ist; nur bei geändertem Namen zeigt er den Serverstand mit Hinweis.
  Ändern und Zurücksetzen der manuellen Bereichsreihenfolge löst keine Live-Meldung aus: Andere Geräte
  sehen sie erst nach der nächsten Laden-, Listen- oder Abhak-Änderung oder nach einem Resync.
- Der Server entscheidet Konflikte (Outcome), statt abzulehnen. Bei einem `PUT` gewinnt der eingehende
  Zustand immer (`NoConflict`, `LastWriteWins`, `ChangeWinsOverDelete`, `CheckedWinsOverDelete`), außer bei
  `CheckedWins` mit **un**abgehakter Anfrage: dann behält der Server seinen abgehakten Stand. Ein `DELETE`
  gilt nur bei `NoConflict`/`BothDeleted`. Die UI (`ShoppingOutcomes`) zeigt nur dann einen Hinweis und
  lässt den Eintragsdialog offen (mit dem Stand des Servers), wenn die Änderung verworfen bzw. das Löschen
  nicht angewendet wurde. Beim Zusammenführen folgt auf eine angewendete Summe immer das `DELETE`
  (sonst stünde die Summe neben dem Duplikat); Hinweise „nichts zusammengeführt“ bzw. „Duplikat nicht
  entfernt“, bei Erfolg Ansage „{Text} zusammengeführt“ und Fokus ins Eingabefeld.
- Eingeschränkte Mitglieder sehen Listen, Läden und Reihenfolge nur lesend, dürfen ergänzen und abhaken;
  der Eintragsdialog zeigt für sie nur die Werte.
- Zusammenführen doppelter Produkte: erster Eintrag per `PUT` mit summierter Menge (gleiche Einheit oder
  beide ohne Menge), zweiter per `DELETE` mit seiner Version.

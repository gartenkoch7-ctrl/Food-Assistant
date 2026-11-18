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

### Abschluss im Detail (M1 F)
- API: `GET …/shopping/lists/{listId}/checkout` (Vorschlag), `POST …` mit `Idempotency-Key` (Abschluss);
  beide ab Mitglied, Eingeschränkte bekommen 403.
- **Vorbelegung** (`CheckoutPrefill`, Domain):
  - Menge: Menge des Eintrags, sonst 1 × Standardeinheit des Produkts, bei Freitext 1 Stück.
  - Lagerort: Standard-Lagerort des Produkts → Ort der zuletzt angelegten (noch vorhandenen) Charge →
    erster Ort (nach `SortOrder`, Name) vom Standard-Lagertyp der Kategorie → erster Ort. Ohne Lagerort
    im Haushalt kein Vorschlag; der Abschluss verlangt dann einen (400 `lines[i].storageLocationId`).
  - Ablaufdatum: bekannter „letzter Kauf“ (`Product.LastShelfLifeDays` + `LastExpiryKind`) → heute +
    Tage mit dieser Art; sonst im Gefrierort Regel „gefroren“ ab heute als Verbrauchsdatum
    (`expiryFromRule`, F5 „selbst eingefroren“), ohne Regel kein Datum; sonst kein Datum.
  - Ein Eintrag mit archiviertem Produkt erscheint wie Freitext (ohne `productId`).
- **Abschluss:** je Zeile `skip` oder Charge (Herkunft `Purchase`, Regeln wie „Neue Charge“ inkl. F5)
  für ein bestehendes oder ein neu angelegtes Produkt (Name, Kategorie, Standardeinheit). Ein Datum aus
  der Regel schickt der Client **nicht** zurück (ohne Datum = selbst eingefroren); die Art eines
  eingetragenen Datums ist Pflicht (keine Vorbelegung).
- **Letzter Kauf** wird nur aus einem vom Nutzer eingetragenen Packungsdatum gelernt (Charge bleibt
  verschlossen, Datum nicht in der Vergangenheit, höchstens 3650 Tage – „läuft nie ab“ wie 31.12.9999
  wird nicht gelernt), nie aus einem Regeldatum.
- **Eine Transaktion** mit Änderungsprotokoll-Sperre des Haushalts zuerst. Unter der Sperre werden alle
  Einträge erneut geprüft (noch auf der Liste, abgehakt, nicht gelöscht, gleiche `Version`) und die
  Produkte (nicht archiviert). Was nicht passt, wird nicht verarbeitet und als `staleItemIds` gemeldet –
  die übrigen Zeilen werden trotzdem gebucht. Ein inzwischen gelöschter Lagerort oder einer, der den
  Lagertyp gewechselt hat (die Charge wurde für den alten Typ gebaut), ergibt 409 `common.conflict` ohne Änderung.
- Verarbeitete Einträge (auch übersprungene) bekommen `DeletedAt` und `Version + 1`; je Charge eine
  Aktivität `batch_added` (Rückgängig wie beim Einbuchen). Danach laufen alle `IStockChangedHandler`
  einmal mit allen betroffenen Produkten.
- Eine Wiederholung mit gleichem `Idempotency-Key` bekommt die gespeicherte Antwort; ohne Schlüssel
  findet sie nur noch gelöschte Einträge und meldet sie als `staleItemIds` (keine Doppelbuchung).
- **Oberfläche** (`/shopping/{id}/complete`, F2): je Eintrag eine Zeile mit Überschrift (Name), Menge,
  Datum mit Art als Text („aus Regel“ ausgeschrieben), Lagerort-Auswahl und „Ändern“ (Dialog: Menge, Ort,
  Datum + Art ohne Vorbelegung, im Gefrierort „selbst eingefroren“). Freitext: `ProductPicker` (legt ein
  neues Produkt über den Katalog an) oder „Überspringen“; ein zugeordnetes Produkt übernimmt bei
  „1 Stück“ ohne Menge im Eintrag (`quantityFromEntry = false`) seine Standardeinheit und – solange der
  Nutzer keinen Ort gewählt hat – den ersten Ort vom Lagertyp seiner Kategorie. Meldet der Server `lines[i].productId` (Produkt inzwischen archiviert),
  bietet die Zeile wieder den `ProductPicker` an. Vor dem Senden liest die Seite die Lagerorte neu: Hat
  einer seit dem Laden den Lagertyp gewechselt, werden die Zeilen dort „umgezogen“ (beim Wechsel zwischen
  Gefrierort und anderem fallen nicht selbst eingetragene Daten weg) und erst angezeigt statt gesendet.
  Ein anderes Produkt in einer Zeile nimmt deren Vorschläge (letzter Kauf, Regeldatum, Standardeinheit
  bei nicht geänderter Standardmenge) nicht mit. Auf 409
  `common.conflict` (Lagerort zwischen Planung und Speichern geändert) lädt die Seite Vorschlag und
  Lagerorte neu. Ein Datum, das der
  Nutzer nicht selbst eingetragen hat (Regel „gefroren“, letzter Kauf), fällt weg, sobald die Zeile
  zwischen Gefrierort und anderem Ort wechselt – ein Gefrierdatum darf nie im Kühlschrank landen.
  **Offen:** Der Server merkt sich beim letzten Kauf nicht den Lagertyp; wurde zuletzt TK-Ware gekauft,
  schlägt er deren Tage auch für einen Kühlschrank-Vorschlag vor (Entscheidung nötig, z. B. Spalte
  `LastStorageType` oder nur aus nicht gefrorenen Chargen lernen).
  **Offen:** Ein beim Abschluss entfernter Eintrag ist ein normaler Tombstone. Ändert ein anderes Gerät ihn
  danach mit älterer `baseVersion` (online mit altem Stand oder offline), gewinnt nach ADR-0005 die Änderung
  gegen das Löschen; der Eintrag kommt abgehakt zurück und würde beim nächsten Abschluss erneut gebucht.
  Lösung braucht eine Entscheidung (z. B. Tombstone als „gebucht“ markieren – Schemaänderung – und im
  `ShoppingConflictResolver` gegen alte Änderungen gewinnen lassen oder nur unabgehakt zurückholen). Der Client nimmt denselben `Idempotency-Key` nur für dieselbe Anfrage nach einem Fehler
  ohne Antwort (Netz/offline oder 409 `common.idempotency_request_in_progress`); jede andere Anfrage bekommt einen neuen (sonst 422 `common.idempotency_key_reused`).
  Erfolg: Ansage + Snackbar, zurück zur Liste; mit `staleItemIds` bleibt die Seite, lädt neu und nennt die Anzahl.

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

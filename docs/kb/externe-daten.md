# Externe Daten

## Open Food Facts
- Zweck: Barcode → Produktname, Marke, Kategorie, Packungsgröße (später: Bild, Nährwerte, Allergene).
- Lizenz: Datenbank **ODbL**, Inhalte **Database Contents License**, Bilder **CC BY-SA**.
  Attribution („Daten von Open Food Facts“) in der App sichtbar; Bilder mit Quellenangabe.
- Zugriff nur über den **Server** (nie direkt aus der App), hinter einer Schnittstelle in
  Application, Implementierung in Infrastructure.
- **Cache:** Antworten werden auf dem Server gespeichert (auch „nicht gefunden“, kürzer) und
  periodisch erneuert; ein Barcode wird so pro Server nur selten abgefragt.
- Eigener `User-Agent` nach den Vorgaben von Open Food Facts (App-Name und Version, generische
  Kontaktangabe des Projekts, keine Nutzerdaten).
- Offline: Barcodes, die schon im Haushalt bekannt sind, werden aus dem Cache der App erkannt;
  unbekannte Produkte brauchen Verbindung.
- Allergene und Nährwerte sind **Herstellerangaben aus der Community** – in der UI als solche
  kennzeichnen, nicht als geprüft darstellen.

### Umsetzung in M1 (Spur C1)
- **Endpunkt:** `GET /api/v1/households/{householdId}/food-facts/{barcode}` (alle Rollen).
  Barcode: 8, 12, 13 oder 14 Ziffern, sonst `400 food_facts.invalid_barcode`. Antworten:
  `200` `FoodFactsProductDto`, `404 food_facts.not_found`, `503 food_facts.unavailable`.
- **Aufruf:** `GET {BaseUrl}api/v2/product/{barcode}?fields=code,product_name,product_name_de,product_name_en,generic_name,brands,quantity,product_quantity,product_quantity_unit,categories_tags`.
  `status: 0` und HTTP 404 gelten beide als „nicht gefunden“. Es geht **nur der Barcode** hinaus;
  keine Cookies, keine Nutzerdaten.
- **Felder:** Name (Sprache der Anfrage: `product_name_<de|en>` → `product_name` →
  `generic_name`), erste Marke, Packungstext, Packungsgröße nur bei g/kg/ml/l, Kategorie aus
  `categories_tags` über eine feste Tabelle (`FoodFactsCategoryMap`; spezifisch vor allgemein).
  Keine Allergene und Nährwerte in M1.
- **Keine Bilder in M1** (Entscheidung F7): weder Abruf noch Speicherung, damit entfällt die
  CC-BY-SA-Bildattribution. Die Attribution „Daten von Open Food Facts“ (ODbL / Database
  Contents License) gehört in die App, sobald Vorschläge angezeigt werden (Spur C2).
- **Cache:** serverweit, ohne Haushaltsbezug (Tabelle `FoodFactsCache`). Gefunden 30 Tage
  frisch, nicht gefunden 2 Tage. Abgelaufen → neu abfragen; schlägt das fehl, wird der alte
  Eintrag mit `stale: true` geliefert (bei einem abgelaufenen „nicht gefunden“ bleibt es bei
  `404`, ohne Kennzeichnung). Nichts gecacht und kein Abruf möglich → `503`. Einträge, die
  90 Tage nicht abgefragt wurden, werden beim nächsten Schreiben gelöscht.
- **Drosselung:** eigene Grenze 60 Abrufe pro Minute (Open Food Facts erlaubt 100
  Produktabfragen pro Minute und IP), Zeitlimit 5 s, keine Wiederholung. Abgewiesene Abrufe
  zählen als „nicht verfügbar“.
- **User-Agent:** `FoodAssistant/<Version> (<Kontakt>)`. Kontakt standardmäßig
  `https://github.com/gartenkoch7-ctrl/Food-Assistant` (Entscheidung F8), überschreibbar mit
  `FoodAssistant__OpenFoodFacts__Contact`.
- **Konfiguration** (`FoodAssistant:OpenFoodFacts`): `Enabled`, `BaseUrl` (https), `Contact`,
  `TimeoutSeconds`, `RequestsPerMinute`. `Enabled=false` schaltet die Abfrage ab; dann
  verlässt kein Aufruf den Server.
- **Tests** rufen nie die echte API auf (Fake-Handler). Ein Live-Test läuft nur explizit und
  mit `FOODASSISTANT_LIVE_TESTS=1`.

## Weitere Quellen
Noch keine. Neue Quelle: Lizenz prüfen, hier eintragen, [datenschutz.md](datenschutz.md) ergänzen.

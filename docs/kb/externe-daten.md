# Externe Daten

## Open Food Facts
- Zweck: Barcode → Produktname, Marke, Kategorie, Packungsgröße, Bild, Nährwerte, Allergene.
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

## Weitere Quellen
Noch keine. Neue Quelle: Lizenz prüfen, hier eintragen, [datenschutz.md](datenschutz.md) ergänzen.

# Produkt und Scope

Food-Assistant ist ein selbst gehosteter Küchen- und Vorratsassistent für **einen Haushalt**
(ADR-0001, ADR-0002). Reihenfolge und Stand der Ausbaustufen: [Roadmap](../plan/README.md).

## Funktionsbereiche

### Vorrat (MVP)
- **Produkte + Chargen:** Ein Produkt (Katalog pro Haushalt) hat mehrere Chargen mit eigener
  Menge, Ablaufdatum, Art (MHD/Verbrauchsdatum) und Lagerort. Details:
  [vorrat-und-haltbarkeit.md](vorrat-und-haltbarkeit.md), ADR-0010.
- **Lagerorte** frei anlegbar, jeder mit Typ (gekühlt, gefroren, Raumtemperatur).
- **Haltbarkeitsregeln** mitgeliefert und anpassbar: geöffnet, eingefroren/aufgetaut, Reste/
  Selbstgemachtes, Teilverbrauch. Die KI schlägt **nie** Haltbarkeiten vor.
- **Erfassen:** Barcode-Scan (Open Food Facts, serverweiter Cache), manuell, beim Einbuchen nach
  dem Einkauf.
- **Verbrauch:** Antippen (aufgebraucht, teilweise, entsorgt mit Grund), Scan „Verbraucht“
  (älteste Charge zuerst); später Kochen (M2) und Home Assistant/Sprache (M4).
- **Erinnerungen** vor Ablauf (ADR-0006).
- **Verschwendungs-Statistik:** pro Monat verbraucht vs. entsorgt, Gründe, Top-Produkte, Trend;
  haushaltsweit. Geldwert ab M5 (Preise).
- **Etiketten als PDF** (Avery-Bögen u. ä.): Name, Datum eingefroren/zubereitet, Ablauf,
  QR-Code der Charge. Direkter Labeldrucker ab M5.

### Einkauf (MVP)
- **Mehrere Einkaufslisten**, optional je einem **Laden** zugeordnet; pro Laden eine
  **Bereichsreihenfolge**, die aus dem Abhaken gelernt wird und anpassbar ist.
- **Offline abhaken**, Live-Updates (ADR-0005).
- **Einkauf abschließen:** gekaufte Artikel werden gesammelt in den Vorrat gebucht – Ablaufdaten
  vorbelegt (Regel bzw. letzter Kauf), schnell korrigierbar.
- **Mindestbestand:** fällt der Bestand darunter, kommt das Produkt automatisch auf die
  Standardliste (markiert „automatisch“).
- **Wiederkehrende Artikel** (täglich/wöchentlich/monatlich, Wochentag).
- Details: [einkauf.md](einkauf.md).

### Import (MVP: Grocy, CSV)
- **Grocy** (über dessen API): Produkte, Bestände, Lagerorte, Einkaufslisten.
- **CSV** für Produkte und Vorrat.
- Später: **Mealie/Tandoor** (Rezepte, M2), **Bring!** nur per eingefügtem Text (keine
  inoffizielle Schnittstelle, M5).

### Rezepte und Essensplan (M2)
- Rezepte gehören dem Haushalt; einzelne können mit anderen Haushalten des Servers geteilt
  (kopiert) oder als Datei exportiert werden.
- Zutaten mit Menge + Einheit, wenn möglich mit einem Produkt verknüpft; sonst Text.
- „Habe ich alles?“ gegen den Vorrat, Portionen umrechnen.
- **Wochenplan** mit frei benennbaren Mahlzeiten (Standard Frühstück, Mittag, Abend); pro Eintrag
  Rezept oder Freitext, Portionen, wer isst mit, wer kocht.
- **„Einkauf für diese Woche“:** Zutaten aller geplanten Gerichte minus Vorrat (inkl. was bis
  dahin abläuft) → Vorschau → Einkaufsliste.
- **Gekocht:** zieht Zutaten vom Vorrat ab, mit Vorschau zum Korrigieren.

### KI (M3, nur mit konfiguriertem Anbieter)
- **Rezeptideen aus dem Vorrat**, bald Ablaufendes bevorzugt, Allergien/Vorlieben beachtet.
- **Rezept-Import** aus Link, Foto oder Text.
- **Kochmodus** Schritt für Schritt mit Rückfragen („womit ersetze ich Sahne?“).
- **Wochenplan-Vorschlag** (Vorrat, Vorlieben, Aufwand; ab M5 auch Budget).
- **Kassenbon-Scan:** Foto → Artikel, Mengen, Preise → Zuordnung prüfen → Vorrat und Preisverlauf.
- Grenzen: [ki-assistent.md](ki-assistent.md), AGENTS.md §2.3.

### Home Assistant und Sprache (M4)
- Eigene **HACS-Integration** mit API-Token: Sensoren (bald ablaufend, Listenanzahl),
  **Todo-Entitäten** für Einkaufslisten, Dienste.
- **Sprache über Home Assistant Assist** (Intents): auf die Liste setzen, „was läuft ab?“, als
  verbraucht markieren. Kein Mikrofon in der App.

### Personen, Geld, Extras (M5)
- **Preise und Budget:** Preise aus Bon oder manuell, Preisverlauf, Monatsbudget für Lebensmittel,
  Verschwendung in €.
- **Allergien und Unverträglichkeiten** pro Person (14 EU-Hauptallergene + eigene): **warnen, nie
  verstecken**; KI-Vorschläge schließen sie aus. Hinweis auf unvollständige Herstellerdaten.
- **Ernährungsvorlieben** pro Person (vegetarisch, vegan, halal, Abneigungen).
- **Nährwerte** pro Rezeptportion und Tagessumme im Plan; kein Tracking, keine Ziele.
- Allergien, Vorlieben und Nährwerte sind **pro Haushalt abschaltbar**; ohne Eintrag werden keine
  Gesundheitsdaten gespeichert.
- **Labeldrucker** (Brother QL im Netzwerk) direkt vom Server.

## Bewusst nicht im Produkt
- Kalorien-/Makro-Tracking und Ernährungsziele.
- Öffentliche Rezept-Community über Server-Grenzen hinweg.
- Inoffizielle Schnittstellen fremder Dienste (z. B. Bring!).
- KI-Aussagen zu Haltbarkeit oder Verzehr nach Ablauf.
- Eigene Spracherkennung in der App.

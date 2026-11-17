# Glossar

| Begriff | Bedeutung | Code |
|---|---|---|
| **Haushalt** | Mandant: Gruppe von Personen, die Vorrat, Einkaufslisten und Rezepte teilen | `Household` |
| **Mitgliedschaft** | Zugehörigkeit einer Person zu einem Haushalt mit Rolle | `HouseholdMembership` |
| **Server-Admin** | verwaltet die Installation, nicht automatisch Haushaltsmitglied | `ServerAdmin` |
| **Besitzer / Mitglied / Eingeschränkt** | Rollen im Haushalt (ADR-0002) | `HouseholdRole` |
| **Gerät** | eine angemeldete App- oder Browser-Sitzung einer Person | `Device` |
| **API-Token** | persönliches Token mit Rechtebereichen für Fremdprogramme | `PersonalAccessToken` |
| **Vorrat** | alle im Haushalt vorhandenen Lebensmittel | `Inventory` |
| **Vorratsposten** | veraltet, siehe Charge | – |
| **MHD** | Mindesthaltbarkeitsdatum – „mindestens haltbar bis“, danach oft noch genießbar | `BestBefore` |
| **Verbrauchsdatum** | „zu verbrauchen bis“ – danach nicht mehr verzehren | `UseBy` |
| **Lagerort** | Kühlschrank, Gefrierschrank, Vorratsschrank … | `StorageLocation` |
| **Einkaufsliste** | Liste zu kaufender Artikel, offline abhakbar | `ShoppingList` |
| **Menge** | Wert mit Einheit | `Quantity` |
| **Änderungswarteschlange** | offline gesammelte Änderungen der App, die beim Sync gesendet werden | `OutboxEntry` |
| **Tombstone** | Löschmarker für den Delta-Sync | `Tombstone` |
| **KI-Budget** | monatliche Obergrenze für KI-Nutzung pro Haushalt | `AiBudget` |
| **Produkt** | Eintrag im Produktkatalog des Haushalts (Name, Barcodes, Kategorie, Mindestbestand) | `Product` |
| **Charge** | eine konkrete Menge eines Produkts mit eigenem Ablaufdatum, Lagerort und Zustand | `StockBatch` |
| **Zustand** | verschlossen, geöffnet, eingefroren, aufgetaut, Rest | `BatchState` |
| **Lagerort-Typ** | gekühlt, gefroren, Raumtemperatur – steuert Haltbarkeitsregeln | `StorageType` |
| **Haltbarkeitsregel** | Tage nach Öffnen/Einfrieren/Auftauen/als Rest je Kategorie und Lagerort-Typ | `ShelfLifeRule` |
| **FEFO** | „first expired, first out“ – Verbrauch nimmt die zuerst ablaufende Charge | – |
| **Verbrauchsereignis** | aufgebraucht, teilweise verbraucht oder entsorgt (mit Grund) | `ConsumptionEvent` |
| **Mindestbestand** | Untergrenze, ab der ein Produkt automatisch auf die Standardliste kommt | `MinimumStock` |
| **Standardliste** | Einkaufsliste, die Mindestbestand, Sprache und Schnellaktionen nutzen | `DefaultShoppingList` |
| **Laden / Bereich** | Geschäft mit gelernter Reihenfolge seiner Bereiche (Obst, Kühlregal …) | `Store` / `StoreSection` |
| **Einkauf abschließen** | gekaufte Einträge gesammelt als Chargen in den Vorrat buchen | `CompleteShoppingTrip` |

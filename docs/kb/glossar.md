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
| **Vorratsposten** | ein Lebensmittel im Vorrat mit Menge, Ort, Ablaufdatum | `InventoryItem` |
| **MHD** | Mindesthaltbarkeitsdatum – „mindestens haltbar bis“, danach oft noch genießbar | `BestBefore` |
| **Verbrauchsdatum** | „zu verbrauchen bis“ – danach nicht mehr verzehren | `UseBy` |
| **Lagerort** | Kühlschrank, Gefrierschrank, Vorratsschrank … | `StorageLocation` |
| **Einkaufsliste** | Liste zu kaufender Artikel, offline abhakbar | `ShoppingList` |
| **Menge** | Wert mit Einheit | `Quantity` |
| **Änderungswarteschlange** | offline gesammelte Änderungen der App, die beim Sync gesendet werden | `OutboxEntry` |
| **Tombstone** | Löschmarker für den Delta-Sync | `Tombstone` |
| **KI-Budget** | monatliche Obergrenze für KI-Nutzung pro Haushalt | `AiBudget` |

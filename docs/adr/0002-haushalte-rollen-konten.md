# ADR-0002: Haushalte, Rollen und Konten

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Food-Assistant ist ein Self-Hosted-Server für einen Haushalt (ADR-0001). Mehrere Personen teilen
Vorrat und Einkaufsliste. Es gibt keinen E-Mail-Versand (ADR-0006).

## Entscheidung

### Haushalte
- Eine Installation kann **mehrere Haushalte** haben; eingerichtet wird zunächst **einer**.
- Der Server-Admin legt weitere Haushalte an (z. B. für Eltern auf demselben Server).
- Eine Person kann **mehreren Haushalten** angehören und wechselt in der Oberfläche zwischen ihnen.
- Alle fachlichen Daten tragen eine `HouseholdId`; jede Abfrage filtert danach (EF-Core-Query-Filter).

### Rollen
| Rolle | Ebene | Rechte |
|---|---|---|
| **Server-Admin** | Server | Server-Einstellungen, KI-Anbieter und -Budgets, Haushalte anlegen/löschen, Sicherheitsprotokoll, Notfall-Reset |
| **Besitzer** | Haushalt | alles im Haushalt, inkl. Mitglieder einladen, Rollen ändern, entfernen, Reset-Links erzeugen, Haushalt löschen |
| **Mitglied** | Haushalt | alles außer Mitglieder- und Haushaltsverwaltung |
| **Eingeschränkt** | Haushalt | Vorrat und Einkaufsliste ansehen, Einkaufsliste abhaken und ergänzen; nichts löschen, keine Einstellungen |

- Jeder Haushalt hat mindestens einen Besitzer; der letzte Besitzer kann sich nicht herabstufen.
- Rechte werden über **Policies** geprüft (`HouseholdRole`), nie über verstreute Rollenvergleiche.

### Konten anlegen
1. **Einrichtungsassistent** beim ersten Start: legt Server-Admin, ersten Haushalt und dessen
   Besitzer (dieselbe Person) an. Danach ist die freie Registrierung **geschlossen**.
2. **Einladung:** Besitzer erzeugt einen Link bzw. QR-Code – **einmalig**, **7 Tage** gültig,
   **Rolle festgelegt**, jederzeit zurückziehbar. Der QR-Code enthält auch die Server-Adresse
   (App-Kopplung, ADR-0003).
3. **Notweg:** Besitzer oder Server-Admin legen ein Konto direkt mit Startpasswort an (z. B. für
   Kinder ohne eigene Geräte-Einrichtung); das Passwort muss beim ersten Login geändert werden.

### Verlauf und Protokoll
- **Aktivitätsverlauf** pro Haushalt („Anna hat Milch gekauft“), sichtbar für alle Mitglieder,
  **90 Tage** aufbewahrt.
- **Sicherheitsprotokoll** für Server-Admins: Logins, Fehlversuche, Sperren, Rollenänderungen,
  Einladungen, Token-Erzeugung, Resets. Aufbewahrung 180 Tage.
- Beide liegen in der Datenbank, nicht in Logs (Logs enthalten keine Nutzerinhalte, AGENTS.md §2.5).

### Löschen
- Jede Person kann ihr **Konto selbst löschen**. Ihre Einträge im Haushalt bleiben; im Verlauf
  erscheint sie als „ehemaliges Mitglied“. Persönliche Daten (Name, Login, Tokens) werden entfernt.
- Ein **Haushalt** wird nach dem Löschen **30 Tage** aufbewahrt (wiederherstellbar durch Besitzer
  oder Server-Admin) und danach endgültig entfernt. Vor dem Löschen wird der Export angeboten
  (ADR-0007).

## Konsequenzen

- Mandantentrennung muss in jedem Repository und jedem Endpunkt getestet werden.
- Rollenprüfung braucht den aktiven Haushalt im Request (Route `/api/v1/households/{id}/…`).
- Ein Hintergrunddienst räumt abgelaufene Einladungen, Verlauf und gelöschte Haushalte auf.

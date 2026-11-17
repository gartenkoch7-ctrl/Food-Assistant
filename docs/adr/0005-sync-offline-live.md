# ADR-0005: Sync, Offline und Live-Updates

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Im Supermarkt fehlt oft das Netz. Gleichzeitig kaufen manchmal zwei Personen parallel ein.
Volle Offline-Fähigkeit mit Konfliktauflösung für alle Daten wäre sehr aufwendig.

## Entscheidung

### Offline-Umfang (Mobil)
- **Lesen offline:** Vorrat und Einkaufslisten des aktiven Haushalts.
- **Schreiben offline:** nur Einkaufsliste – Einträge **abhaken**, **ergänzen**, **Menge ändern**,
  **entfernen**.
- Alles andere (Vorrat bearbeiten, Rezepte, KI, Einstellungen) braucht Verbindung; die UI zeigt
  das klar an, statt zu scheitern.
- Lokaler Speicher: **SQLite** auf dem Gerät, nur Cache plus **Änderungswarteschlange**. Die
  Wahrheit liegt im Server.
- Das Web (PWA) ist im ersten Ausbau **online-only**; der Service Worker cached nur die App-Hülle.

### Synchronisation
- Abruf per **Delta**: `GET …/changes?since=<token>` liefert geänderte und gelöschte Einträge
  (Tombstones, 30 Tage aufbewahrt) sowie ein neues `since`-Token. Ist das Token zu alt → volle
  Neuladung.
- Die Warteschlange sendet Änderungen in Reihenfolge, jede mit **Idempotency-Key** (ADR-0004),
  vom Client erzeugter **UUIDv7** für neue Einträge und der zuletzt bekannten Version.

### Konfliktregeln Einkaufsliste
| Fall | Ergebnis |
|---|---|
| Abhaken vs. beliebige Änderung | **Abhaken gewinnt** (gekauft ist gekauft) |
| Zwei neue Einträge (auch gleicher Artikel) | **beide** übernommen; die UI schlägt Zusammenführen vor |
| Menge/Notiz zweimal geändert | **letzte Änderung gewinnt** (Server-Zeitpunkt des Eingangs) |
| Gelöscht vs. geändert | **Änderung gewinnt**, Eintrag bleibt bestehen |
| Gelöscht vs. abgehakt | **abgehakt** bleibt bestehen |

- Konflikte werden **ohne Nachfrage** aufgelöst; der Aktivitätsverlauf (ADR-0002) zeigt, was
  passiert ist.
- Die Regeln liegen als reine Logik in **Domain** und sind vollständig unit-getestet.

### Live-Updates
- **SignalR**-Hub pro Haushalt: Server meldet Änderungen an Einkaufsliste und Vorrat an alle
  verbundenen Web- und App-Clients (nur IDs + Version, Client holt Details per API/Delta).
- Hub-Verbindung mit demselben Login (Cookie bzw. Access-Token); Gruppenbeitritt nur für
  Haushalte, in denen die Person Mitglied ist.
- Bei Verbindungsverlust: automatische Wiederverbindung, danach Delta-Abruf.

## Konsequenzen

- Einkaufslisten-Einträge brauchen Version, `CheckedAt`, `UpdatedAt` (Server) und Tombstones.
- Die App braucht EF Core SQLite oder `sqlite-net` als lokalen Speicher – Entscheidung im
  Detailplan, nicht im Domain-Code.
- Ein späterer Ausbau (mehr Offline-Schreiben, PWA offline) braucht ein neues ADR.
